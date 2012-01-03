using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;

namespace FastCouch
{
    public class HttpClient : IDisposable
    {
        private static BufferPool<byte> BufferPool = new BufferPool<byte>(32, 4096);

        public string HostName { get; private set; }
        public int Port { get; private set; }
        private bool _hasBeenDisposed;
        private readonly object _gate = new object();
        private Action<string, HttpCommand> _onFailure;

        public HttpClient(string hostName, int port, Action<string, HttpCommand> onFailure)
        {
            HostName = hostName;
            Port = port;
            
            _onFailure = onFailure;
        }

        public bool TrySend(HttpCommand command)
        {
            lock (_gate)
            {
                if (_hasBeenDisposed == true)
                    return false;
            }

            command.UriBuilder.Host = this.HostName;
            command.UriBuilder.Port = this.Port;

            var request = (HttpWebRequest)HttpWebRequest.Create(command.UriBuilder.Uri);
            
            command.HttpReadState = new HttpReadState(request);
            
            try
            {
                request.BeginGetResponse(OnBeginGetResponseCompleted, command);
            }
            catch
            {
                OnFailure(command);
                return false;
            }

            return true;
        }

        private void OnBeginGetResponseCompleted(IAsyncResult result)
        {
            HttpCommand command = (HttpCommand)result.AsyncState;
            try
            {
                HttpWebRequest request = command.HttpReadState.WebRequest;
                var response = request.EndGetResponse(result);
                
                var stream = response.GetResponseStream();
                
                command.HttpReadState.Stream = stream;
                command.HttpReadState.Buffer = BufferPool.Get();
                
                BeginRead(command);
            }
            catch
            {
                OnFailure(command);
            }
        }

        private void BeginRead(HttpCommand command)
        {
            try
            {
                var stream = command.HttpReadState.Stream;

                while (command.HttpReadState.HasStillMoreBytesToRead)
                {
                    ArraySegment<byte> buffer = command.HttpReadState.Buffer;
                    var result = stream.BeginRead(buffer.Array, buffer.Offset, buffer.Count, OnBeginReadCompleted, command);

                    if (!result.CompletedSynchronously)
                        return;

                    EndRead(command, result);
                }
            }
            catch
            {
                OnFailure(command);
            }
        }
       
        private void OnBeginReadCompleted(IAsyncResult result)
        {
            if (result.CompletedSynchronously)
                return;

            var command = (HttpCommand)result.AsyncState;

            try
            {
                EndRead(command, result);
                BeginRead(command);
            }
            catch
            {
                OnFailure(command);
            }
        }
        
        private void OnFailure(HttpCommand command)
        {
            CleanupHttpReadState(command);
            _onFailure(this.HostName, command);
        }
        
        private static void CleanupHttpReadState(HttpCommand command)
        {
            if (command.HttpReadState.StringDecoder != null)
            {
                command.HttpReadState.StringDecoder.Dispose();
            }

            BufferPool.Return(command.HttpReadState.Buffer);
            
            command.HttpReadState = new HttpReadState();
        }

        private void EndRead(HttpCommand command, IAsyncResult result)
        {
            var stream = command.HttpReadState.Stream;
            var bytesRead = stream.EndRead(result);

            if (bytesRead > 0)
            {
                var buffer = command.HttpReadState.Buffer;

                command.HttpReadState.StringDecoder.Decode(new ArraySegment<byte>(buffer.Array, buffer.Offset, bytesRead));

            }
            else
            {
                command.Value = command.HttpReadState.StringDecoder.ToString();

                CleanupHttpReadState(command);

                command.NotifyComplete();
            }
        }

        public void Dispose()
        {
            lock(_gate)
            {
                _hasBeenDisposed = true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;

namespace FastCouch
{
    public class HttpClient : IDisposable
    {

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

            command.SetHost(this.HostName, this.Port);

            var request = (HttpWebRequest)HttpWebRequest.Create(command.UriBuilder.Uri);

            command.BeginRequest(request);            
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

                command.OnGotResponse(stream);                
                
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
            command.EndReading();
            _onFailure(this.HostName, command);
        }

        private void EndRead(HttpCommand command, IAsyncResult result)
        {
            var stream = command.HttpReadState.Stream;
            var bytesRead = stream.EndRead(result);

            if (bytesRead > 0)
            {

                command.OnRead(bytesRead);
            }
            else
            {
                command.EndReading();
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

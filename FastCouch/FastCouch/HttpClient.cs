using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Threading;

namespace FastCouch
{
    public class HttpClient : IDisposable
    {
        public string HostName { get; private set; }
        public int Port { get; private set; }
        private bool _hasBeenDisposed;
        private readonly object _gate = new object();
        private Action<string, HttpCommand> _onFailure;

        HashSet<HttpCommand> _pendingCommands = new HashSet<HttpCommand>();

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
                {
                    return false;
                }
                _pendingCommands.Add(command);
            }

            command.SetHost(this.HostName, this.Port);

            try
            {
                var request = (HttpWebRequest)HttpWebRequest.Create(command.UriBuilder.Uri);
                command.BeginRequest(request);

                var result = request.BeginGetResponse(OnBeginGetResponseCompleted, command);

                //ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, (object state, bool timedOut) =>
                //    {
                //        if (timedOut)
                //        {
                //            var httpCommand = (HttpCommand)state;
                //            httpCommand.HttpReadState.WebRequest.Abort();
                //        }
                //    }, command, TimeSpan.FromSeconds(10), true);

                return true;
            }
            catch
            {
            }
            return false;
        }


        private void OnBeginGetResponseCompleted(IAsyncResult result)
        {
            HttpCommand command = (HttpCommand)result.AsyncState;
            try
            {
                HttpWebRequest request = command.HttpReadState.WebRequest;

                var response = request.EndGetResponse(result);
                var stream = response.GetResponseStream();
                command.OnGotResponse(response, stream);

                BeginRead(command);
                return;
            }
            catch
            {
            }

            OnFailure(command);
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
            
            Thread.MemoryBarrier();

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
            
            RemoveFromPendingCommands(command);

            _onFailure(this.HostName, command);
        }
        
        private void RemoveFromPendingCommands(HttpCommand command)
        {
            lock (_gate)
            {
                _pendingCommands.Remove(command);
            }
        }

        private void EndRead(HttpCommand command, IAsyncResult result)
        {
            var stream = command.HttpReadState.Stream;
            var bytesRead = stream.EndRead(result);

            if (bytesRead > 0 && 
                command.OnRead(bytesRead))
            {
                return;
            }

            command.EndReading();
            RemoveFromPendingCommands(command);

            command.NotifyComplete();
        }

        public void Dispose()
        {
            List<HttpCommand> copyOfPendingCommands;
            lock (_gate)
            {
                _hasBeenDisposed = true;
                copyOfPendingCommands = _pendingCommands.ToList();
            }

            for (int i = 0; i < copyOfPendingCommands.Count; i++)
            {
                HttpCommand command = copyOfPendingCommands[i];
                //Abort(command);
            }
        }
    }
}

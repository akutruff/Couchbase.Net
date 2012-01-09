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

        AsyncPattern<HttpWebRequest, HttpCommand> _getResponseAsync;
        AsyncPattern<Stream, HttpCommand> _readAsync;

        HashSet<HttpCommand> _pendingCommands = new HashSet<HttpCommand>();

        public HttpClient(string hostName, int port, Action<string, HttpCommand> onFailure)
        {
            HostName = hostName;
            Port = port;

            _onFailure = onFailure;

            _getResponseAsync = new AsyncPattern<HttpWebRequest, HttpCommand>(
                (request, pattern, command) => request.BeginGetResponse(pattern.OnCompleted, command),
                OnBeginGetResponseCompleted,
                OnBeginGetResponseFailed);

            _readAsync = new AsyncPattern<Stream, HttpCommand>(
                (stream, pattern, command) => 
                    {
                        ArraySegment<byte> buffer = command.HttpReadState.Buffer;
                        return stream.BeginRead(buffer.Array, buffer.Offset, buffer.Count, pattern.OnCompleted, command);
                    },
                OnBeginReadCompleted,
                OnBeginReadFailed);
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
            var request = (HttpWebRequest)HttpWebRequest.Create(command.UriBuilder.Uri);

            command.BeginRequest(request);

            return _getResponseAsync.BeginAsync(request, command);
        }
                
        private AsyncPatternResult<HttpWebRequest, HttpCommand> OnBeginGetResponseCompleted(IAsyncResult result, HttpCommand command)
        {
            HttpWebRequest request = command.HttpReadState.WebRequest;

            var response = request.EndGetResponse(result);
            var stream = response.GetResponseStream();
            command.OnGotResponse(response, stream);

            BeginRead(command);

            return _getResponseAsync.Stop();
        }

        private AsyncPatternResult<HttpWebRequest, HttpCommand> OnBeginGetResponseFailed(IAsyncResult result, HttpCommand command, Exception e)
        {
            OnFailure(command);
            return _getResponseAsync.Stop();
        }

        private bool BeginRead(HttpCommand command)
        {
            if (command.HttpReadState.HasStillMoreBytesToRead)
            {
                var stream = command.HttpReadState.Stream;
                return _readAsync.BeginAsync(stream, command);
            }
            else
            {
                return false;
            }
        }

        private AsyncPatternResult<Stream, HttpCommand> OnBeginReadCompleted(IAsyncResult result, HttpCommand command)
        {
            var stream = command.HttpReadState.Stream;
            var bytesRead = stream.EndRead(result);

            if (bytesRead > 0 &&
                command.OnRead(bytesRead) &&
                command.HttpReadState.HasStillMoreBytesToRead)
            {
                return _readAsync.Continue(command.HttpReadState.Stream, command);
            }

            command.EndReading();
            RemoveFromPendingCommands(command);

            command.NotifyComplete();

            return _readAsync.Stop();
        }

        private AsyncPatternResult<Stream, HttpCommand> OnBeginReadFailed(IAsyncResult result, HttpCommand command, Exception e)
        {
            OnFailure(command);
            return _readAsync.Stop();
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

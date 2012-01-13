using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace FastCouch
{
    public class MemcachedClient : IDisposable
    {
        private TcpClient _tcpClient;

        private ResponseStreamReader _responseReader;
        private RequestStreamWriter _requestWriter;

        private bool _hasBeenDisposed;

        private readonly object _gate = new object();

        public event Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> OnDisconnected;
        public event Action<string, MemcachedCommand> OnRecoverableError;

        private NetworkStream _stream;

        private readonly Queue<MemcachedCommand> _pendingSends = new Queue<MemcachedCommand>();

        private readonly Dictionary<int, MemcachedCommand> _pendingReceives = new Dictionary<int, MemcachedCommand>();

        public string HostName { get; private set; }
        public string ServerId { get; private set; }

        private readonly int _port;

        private bool _isWriterConnectionOpen;
        private bool _isReaderConnectionOpen;

        public MemcachedClient(string serverId, string hostName, int port)
        {
            ServerId = serverId;
            this.HostName = hostName;
            _port = port;
        }

        public void Connect()
        {
            var tcpClient = new TcpClient(HostName, _port);
            Connect(tcpClient);
        }

        public void Connect(TcpClient tcpClient)
        {
            lock (_gate)
            {
                if (_stream != null)
                {
                    throw new Exception("Already connected");
                }

                _tcpClient = tcpClient;
                _stream = _tcpClient.GetStream();

                _isReaderConnectionOpen = true;
                _isWriterConnectionOpen = true;

                _requestWriter = new RequestStreamWriter(_stream, GetNextCommandAfterPreviousSendCompleted, OnWriterDisconnected);
                _responseReader = new ResponseStreamReader(_stream, GetPendingReceiveCommandById, OnCommandResponseReceived, OnErrorReceived, OnResponseStreamReaderDisconnected);
            }
        }

        private MemcachedCommand GetNextCommandAfterPreviousSendCompleted(MemcachedCommand command)
        {
            lock (_gate)
            {
                _pendingSends.Dequeue();
                if (_isWriterConnectionOpen && _isReaderConnectionOpen && !_hasBeenDisposed && _pendingSends.Count > 0)
                {
                    var commandBeingSent = _pendingSends.Peek();
                    _pendingReceives[commandBeingSent.Id] = commandBeingSent;
                    return commandBeingSent;
                }

                return null;
            }
        }

        private void OnResponseStreamReaderDisconnected()
        {
            lock (_gate)
            {
                _isReaderConnectionOpen = false;
            }

            HandleDisconnection();
        }

        private void OnWriterDisconnected()
        {
            lock (_gate)
            {
                _isWriterConnectionOpen = false;
            }

            HandleDisconnection();
        }

        private void HandleDisconnection()
        {
            IEnumerable<MemcachedCommand> pendingSends = null;
            IEnumerable<MemcachedCommand> pendingReceives = null;

            Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> callbackForWhenBothReaderAndWriterHaveDisconnected = null;

            lock (_gate)
            {
                var isWriterTerminated = _pendingSends.Count == 0 || !_isWriterConnectionOpen;
                var isReaderTerminated = _pendingReceives.Count == 0 || !_isReaderConnectionOpen;

                if (isReaderTerminated && isWriterTerminated)
                {
                    //The first item in the pending sends has actually already been passed along the wire so report as pendingReceive rather than pendingSend.
                    pendingSends = _pendingSends.Skip(1).ToList();
                    pendingReceives = _pendingReceives.Values.ToList();

                    callbackForWhenBothReaderAndWriterHaveDisconnected = OnDisconnected;
                    OnDisconnected = null;

                    Dispose();
                }
            }

            if (callbackForWhenBothReaderAndWriterHaveDisconnected != null)
            {
                callbackForWhenBothReaderAndWriterHaveDisconnected(this.ServerId, pendingSends, pendingReceives);
            }
        }

        private MemcachedCommand GetPendingReceiveCommandById(int commandId)
        {
            lock (_gate)
            {
                return _pendingReceives[commandId];
            }
        }

        private void OnCommandResponseReceived(MemcachedCommand command)
        {
            RemoveFromPendingReceives(command);
            command.NotifyComplete();
        }

        private void OnErrorReceived(MemcachedCommand command)
        {
            RemoveFromPendingReceives(command);

            switch (command.ResponseStatus)
            {
                case ResponseStatus.VbucketBelongsToAnotherServer:
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    {
                        var ev = OnRecoverableError;
                        if (ev != null)
                        {
                            ev(this.ServerId, command);
                        }
                    }
                    break;
                case ResponseStatus.NoError:
                    throw new Exception("Should be impossible to get here...");
                default:
                    //The error is not something we can deal with inside the library itself, either the caller screwed up, or there was a catastrophic failure.
                    command.NotifyComplete();
                    break;
            }
        }

        private void RemoveFromPendingReceives(MemcachedCommand command)
        {
            lock (_gate)
            {
                _pendingReceives.Remove(command.Id);
            }
        }

        public bool TrySend(MemcachedCommand command)
        {
            bool isWriterIdle = false;
            lock (_gate)
            {
                if (_hasBeenDisposed)
                    return false;

                var areEitherReaderOrWriterClosed = !_isReaderConnectionOpen || !_isWriterConnectionOpen;

                if (areEitherReaderOrWriterClosed || _hasBeenDisposed)
                {
                    return false;
                }

                _pendingSends.Enqueue(command);

                isWriterIdle = _pendingSends.Count == 1;

                if (isWriterIdle)
                {
                    _pendingReceives[command.Id] = command;
                }
            }

            if (isWriterIdle)
            {
                _requestWriter.Send(command);
            }

            return true;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_hasBeenDisposed)
                {
                    return;
                }

                _hasBeenDisposed = true;

                _tcpClient.SafeClose();
            }
        }
    }
}

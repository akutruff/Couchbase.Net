using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace FastCouch
{
    public class ResponseStreamReader
    {
        private Stream _stream;

        private ReadState _readState = new ReadState();

        private readonly Func<int, MemcachedCommand> _commandRetriever;
        private readonly Action<MemcachedCommand> _onCommandComplete;
        private readonly Action<MemcachedCommand> _onError;
        private readonly Action _onDisconnect;

        //4K should do nicely as a receive window I think.
        private const int ReceiveBufferSize = 4096;

        private readonly byte[] _receiveBuffer = new byte[ReceiveBufferSize];

        private const int ResponseHeaderSize = 24;
        private readonly byte[] _responseHeader = new byte[ResponseHeaderSize];

        private const int MaxExtrasLength = 255;
        private readonly byte[] _extras = new byte[MaxExtrasLength];

        private const int MaxKeyLength = 255;
        private readonly byte[] _key = new byte[MaxKeyLength];

        private Action _currentReadState;

        private int _bytesAvailableFromLastRead;
        private int _currentByteInReceiveBuffer;

        private bool _hasQuit;

        AsyncPattern<Stream> _readAsync;

        public ResponseStreamReader(
            Stream stream,
            Func<int, MemcachedCommand> commandRetriever,
            Action<MemcachedCommand> onCommandComplete,
            Action<MemcachedCommand> onError,
            Action onDisconnect)
        {
            _onError = onError;
            _onCommandComplete = onCommandComplete;
            _commandRetriever = commandRetriever;
            _onDisconnect = onDisconnect;

            _stream = stream;
            _currentReadState = ReadResponseHeader;

            _readAsync = AsyncPattern.Create(
                (strea, pattern) => strea.BeginRead(_receiveBuffer, 0, _receiveBuffer.Length, pattern.OnCompleted, null),
                OnBeginReadCompleted,
                OnBeginReadFailed);

            BeginReading();
        }

        private void BeginReading()
        {
            ThreadPool.QueueUserWorkItem(_ => _readAsync.BeginAsync(_stream));
        }

        private void ResetForNewResponse()
        {
            if (_errorDecoder != null)
            {
            	_errorDecoder.Dispose();
            }

            _readState = new ReadState();
            _currentReadState = ReadResponseHeader;
        }
       
        private Stream OnBeginReadCompleted(IAsyncResult result)
        {
            _bytesAvailableFromLastRead = _stream.EndRead(result);
            _currentByteInReceiveBuffer = 0;

            if (_bytesAvailableFromLastRead > 0)
            {
                while (_currentByteInReceiveBuffer < _bytesAvailableFromLastRead && !_hasQuit)
                {
                    _currentReadState();
                }

                if (!_hasQuit)
                {
                    return _stream;
                }
            }

            Disconnnect();
            return null;
        }

        private Stream OnBeginReadFailed(IAsyncResult result, Exception e)
        {
            Disconnnect();
            return null;
        }

        private void Disconnnect()
        {
            _hasQuit = true;
            _onDisconnect();
        }

        private void ReadResponseHeader()
        {
            BufferUtils.CopyAsMuchAsPossible(_receiveBuffer, ref _currentByteInReceiveBuffer, _bytesAvailableFromLastRead, _responseHeader, ref _readState.CurrentByteOfResponseHeader, _responseHeader.Length);

            if (_readState.CurrentByteOfResponseHeader == _responseHeader.Length)
            {
                const int keyLengthFieldOffset = 2;
                const int extrasLengthFieldOffset = 4;
                const int responseStatusByteOffset = 6;
                const int totalBodyLengthFieldOffset = 8;
                const int opaqueFieldOffset = 12;
                const int casFieldOffset = 16;

                Opcode opcode = (Opcode)_responseHeader[1];
                if (opcode == Opcode.Quit)
                {
                    _hasQuit = true;
                    return;
                }

                var commandId = BitParser.ParseInt(_responseHeader, opaqueFieldOffset);
                
                //Console.WriteLine(opcode.ToString() + " " + (commandId + Int32.MinValue));
                
                MemcachedCommand command = _commandRetriever(commandId);
                _readState.Command = command;

                _readState.KeyLength = BitParser.ParseUShort(_responseHeader, keyLengthFieldOffset);
                _readState.ExtrasLength = _responseHeader[extrasLengthFieldOffset];

                _readState.ResponseStatus = BitParser.ParseResponseStatus(_responseHeader, responseStatusByteOffset);
                command.ResponseStatus = _readState.ResponseStatus;

                //TODO: AK TotalBodyLength should *technically* be a uint and not an int. In the .NET world all lengths are typically ints, not uints.
                //      Do we really care about supporting an extra bits worth of body length?  Who is storing values over 2GB in their memcache database? 

                var totalBodyLength = BitParser.ParseInt(_responseHeader, totalBodyLengthFieldOffset);
                _readState.ValueLength = totalBodyLength - _readState.ExtrasLength - _readState.KeyLength;

                _readState.Cas = BitParser.ParseLong(_responseHeader, casFieldOffset);
                command.Cas = _readState.Cas;

                if (_readState.ResponseStatus == ResponseStatus.NoError)
                {
                    _currentReadState = ReadResponseExtras;
                    ReadResponseExtras();
                }
                else
                {
                    _currentReadState = ReadError;
                    ReadError();
                }
            }
        }

        private void ReadResponseExtras()
        {
            BufferUtils.CopyAsMuchAsPossible(_receiveBuffer, ref _currentByteInReceiveBuffer, _bytesAvailableFromLastRead, _extras, ref _readState.CurrentByteOfExtras, _readState.ExtrasLength);

            if (_readState.CurrentByteOfExtras >= _readState.ExtrasLength)
            {
                _currentReadState = ReadResponseKey;
                ReadResponseKey();
            }
        }

        private void ReadResponseKey()
        {
            BufferUtils.CopyAsMuchAsPossible(_receiveBuffer, ref _currentByteInReceiveBuffer, _bytesAvailableFromLastRead, _key, ref _readState.CurrentByteOfKey, _readState.KeyLength);

            if (_readState.CurrentByteOfKey >= _readState.KeyLength)
            {
                _currentReadState = ReadCommandResponse;
                ReadCommandResponse();
            }
        }

        private void ReadCommandResponse()
        {
            int numberConsumedByCommand = BufferUtils.CalculateMaxPossibleBytesForCopy(
                                              _readState.CurrentByteOfValue,
                                              _readState.ValueLength,
                                              _currentByteInReceiveBuffer,
                                              _bytesAvailableFromLastRead);

            _readState.Command.Parse(
                _readState.ResponseStatus,
                new ArraySegment<byte>(_receiveBuffer, _currentByteInReceiveBuffer, numberConsumedByCommand),
                new ArraySegment<byte>(_extras, 0, _readState.ExtrasLength),
                new ArraySegment<byte>(_key, 0, _readState.KeyLength),
                _readState.CurrentByteOfValue,
                _readState.ValueLength);

            _readState.CurrentByteOfValue += numberConsumedByCommand;
            _currentByteInReceiveBuffer += numberConsumedByCommand;

            if (_readState.CurrentByteOfValue > _readState.ValueLength)
                throw new Exception("We read beyond the seams...");

            if (_readState.CurrentByteOfValue == _readState.ValueLength)
            {
                _onCommandComplete(_readState.Command);
                ResetForNewResponse();
            }
        }

        private StringDecoder _errorDecoder;
        private void ReadError()
        {
            int bytesToCopy = BufferUtils.CalculateMaxPossibleBytesForCopy(_currentByteInReceiveBuffer, _bytesAvailableFromLastRead, _readState.CurrentByteOfValue, _readState.ValueLength);
            
            if (_readState.CurrentByteOfValue == 0)
            {
            	_errorDecoder = new StringDecoder();
            }

            _errorDecoder.Decode(new ArraySegment<byte>(_receiveBuffer, _bytesAvailableFromLastRead, bytesToCopy));
            //_readState.Command.ErrorMessage += System.Text.Encoding.UTF8.GetString(_receiveBuffer, _currentByteInReceiveBuffer, bytesToCopy);

            _currentByteInReceiveBuffer += bytesToCopy;
            _readState.CurrentByteOfValue += bytesToCopy;

            if (_readState.CurrentByteOfValue >= _readState.ValueLength)
            {
                _readState.Command.ErrorMessage = _errorDecoder.ToString();
                _onError(_readState.Command);
                ResetForNewResponse();
            }
        }

        private struct ReadState
        {
            public MemcachedCommand Command;

            public int KeyLength;
            public int CurrentByteOfKey;

            public int ExtrasLength;
            public int CurrentByteOfExtras;

            public ResponseStatus ResponseStatus;
            public int ValueLength;
            public int CurrentByteOfResponseHeader;

            public long Cas;
            public int CurrentByteOfValue;

            public bool HasReadAllDataAfterHeader
            {
                get
                {
                    return
                        this.CurrentByteOfValue >= this.ValueLength &&
                        CurrentByteOfKey >= this.KeyLength &&
                        this.CurrentByteOfExtras >= this.ExtrasLength;
                }
            }
        }
    }
}
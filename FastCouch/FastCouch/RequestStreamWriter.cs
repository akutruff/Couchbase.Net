using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace FastCouch
{
    public class RequestStreamWriter
    {
        private Stream _stream;

        private const int RequestBufferSize = 4096;

        private readonly byte[] _sendBuffer = new byte[RequestBufferSize];
        private readonly char[] _encodingBuffer = new char[RequestBufferSize];

        private Func<MemcachedCommand, MemcachedCommand> _onCommandSent;
        private Action _onDisconnect;
        
        private int _currentByteInSendBuffer;
        private WriteState _writeState;

        public RequestStreamWriter(Stream stream, Func<MemcachedCommand, MemcachedCommand> onCommandSent, Action onDisconnect)
        {
            _stream = stream;
            _onCommandSent = onCommandSent;
            _onDisconnect = onDisconnect;
        }

        public void Send(MemcachedCommand command)
        {
            Thread.MemoryBarrier();
            InitiateCommand(command);            
            BeginWrite();
        }
        
        private void InitiateCommand(MemcachedCommand command)
        {
            WriteCommandHeader(command);
            
            _writeState = new WriteState(command);
        }

        private void WriteCommandHeader(MemcachedCommand command)
        {
            command.BeginWriting();

            WriteRequestHeader(_sendBuffer, command.Opcode, command.RequestHeader);
            
            const int requestHeaderSize = 24;

            _currentByteInSendBuffer += requestHeaderSize;

            command.WriteExtras(new ArraySegment<byte>(_sendBuffer, _currentByteInSendBuffer, command.RequestHeader.ExtrasLength));

            _currentByteInSendBuffer += command.RequestHeader.ExtrasLength;

            command.WriteKey(new ArraySegment<byte>(_sendBuffer, _currentByteInSendBuffer, command.RequestHeader.KeyLength));
            
            _currentByteInSendBuffer += command.RequestHeader.KeyLength;
        }

        private void BeginWrite()
        {
            try
            {
                WriteCommand();
            }
            catch
            {
                _onDisconnect();
            }
        }

        private void WriteCommand()
        {
            IAsyncResult result;
            do
            {
                int bytesToBeWrittenByCommand = BufferUtils.CalculateMaxPossibleBytesForCopy(_writeState.CurrentByteInValue, _writeState.TotalBytesInValue, _currentByteInSendBuffer, _sendBuffer.Length);

                var bodyBuffer = new ArraySegment<byte>(_sendBuffer, _currentByteInSendBuffer, bytesToBeWrittenByCommand);
                var bytesActuallyWritten = _writeState.Command.WriteValue(bodyBuffer, _writeState.CurrentByteInValue);

                _currentByteInSendBuffer += bytesActuallyWritten;
                _writeState.CurrentByteInValue += bytesActuallyWritten;

                int sizeOfActualDataInSendBuffer = _currentByteInSendBuffer;

                result = _stream.BeginWrite(_sendBuffer, 0, sizeOfActualDataInSendBuffer, OnBeginWriteComplete, null);

                if (!result.CompletedSynchronously)
                    return;

            } while (OnSendComplete(result));
        }

        private void OnBeginWriteComplete(IAsyncResult result)
        {
            if (result.CompletedSynchronously)
                return;
            
            Thread.MemoryBarrier();
            
            if (OnSendComplete(result))
            {
                BeginWrite();
            }
        }

        private bool OnSendComplete(IAsyncResult result)
        {
            try
            {
                _stream.EndWrite(result);
                _currentByteInSendBuffer = 0;

                if (_writeState.CurrentByteInValue < _writeState.TotalBytesInValue)
                {
                    return true;
                }
                else
                {
                    var nextCommand = _onCommandSent(_writeState.Command);

                    if (nextCommand != null)
                    {
                        InitiateCommand(nextCommand);
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }                
            }
            catch
            {
                _onDisconnect();
                return false;
            }   
        }

        private static void WriteRequestHeader(Byte[] buffer, Opcode opcode,  MemcachedHeader header)
        {
            buffer[0] = (byte)MagicBytes.RequestPacket;

            buffer[1] = (byte)opcode;

            var keyLength = header.KeyLength;

            buffer[2] = (byte)(keyLength >> 8);
            buffer[3] = (byte)(keyLength);

            buffer[4] = (byte)(header.ExtrasLength);

            const byte dataType = 0;
            buffer[5] = dataType;

            var vBucket = header.VBucket;
            buffer[6] = (byte)(vBucket >> 8);
            buffer[7] = (byte)(vBucket);

            int totalBodyLength = header.TotalBodyLength;
            buffer[8] = (byte)(totalBodyLength >> 24);
            buffer[9] = (byte)(totalBodyLength >> 16);
            buffer[10] = (byte)(totalBodyLength >> 8);
            buffer[11] = (byte)(totalBodyLength);

            var opaque = header.Opaque;
            buffer[12] = (byte)(opaque >> 24);
            buffer[13] = (byte)(opaque >> 16);
            buffer[14] = (byte)(opaque >> 8);
            buffer[15] = (byte)(opaque);

            var cas = header.Cas;
            buffer[16] = (byte)(cas >> 56);
            buffer[17] = (byte)(cas >> 48);
            buffer[18] = (byte)(cas >> 40);
            buffer[19] = (byte)(cas >> 32);
            buffer[20] = (byte)(cas >> 24);
            buffer[21] = (byte)(cas >> 16);
            buffer[22] = (byte)(cas >> 8);
            buffer[23] = (byte)(cas);
        }

        private struct WriteState
        {
            public MemcachedCommand Command;
            public int CurrentByteInValue;
            public int TotalBytesInValue;

            public WriteState(MemcachedCommand command)
            {
                Command = command;
                TotalBytesInValue = command.RequestHeader.TotalBodyLength - command.RequestHeader.KeyLength - command.RequestHeader.ExtrasLength;
                CurrentByteInValue = 0;
            }
        }
    }
}
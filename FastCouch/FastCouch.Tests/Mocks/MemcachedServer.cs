using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;

namespace FastCouch.Tests.Mocks
{
    public class MemcachedServer : IDisposable
    {
        private const int headerSize = 24;
        
        public int Port { get; private set; }
        public string HostName { get; private set; }

        private NetworkStream _stream;
        private TcpClient _tcpClient;
        private TcpListener _tcpListener;
        private AsyncPattern<TcpListener> _acceptTcpClientAsync;

        private AsyncPattern<Stream> _readAsync;

        private object _gate = new object();

        private HashSet<int> _vBuckets = new HashSet<int>();

        private byte[] _writeBuffer = new byte[4096];

        private byte[] _readBuffer = new byte[4096];

        private int _currentByteInReadBuffer;

        public MemcachedServer(string hostName, int port)
        {
            HostName = hostName;
            this.Port = port;

            _acceptTcpClientAsync = AsyncPattern.Create((listener, pattern) => listener.BeginAcceptTcpClient(pattern.OnCompleted, null), OnTcpClient, OnAcceptError);
            _readAsync = AsyncPattern.Create<Stream>((stream, pattern) => stream.BeginRead(_readBuffer, _currentByteInReadBuffer, _readBuffer.Length - _currentByteInReadBuffer, pattern.OnCompleted, null), OnRead, OnReadError);

            try
            {
                _tcpListener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
                _tcpListener.Start();

                _acceptTcpClientAsync.BeginAsync(_tcpListener);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void SetVbuckets(IEnumerable<int> vBuckets)
        {
            lock(_gate)
            {
                _vBuckets = new HashSet<int>(vBuckets);
            }
        }

        private TcpListener OnTcpClient(IAsyncResult result)
        {
            _tcpClient = _tcpListener.EndAcceptTcpClient(result);
            _stream = _tcpClient.GetStream();

            _readAsync.BeginAsync(_stream);

            return _acceptTcpClientAsync.Stop();
        }

        private TcpListener OnAcceptError(IAsyncResult result, Exception e)
        {
            return _acceptTcpClientAsync.Stop();
        }

        private void WriteGetResponse(int opaque, string value)
        {
            var valueLength = Encoding.UTF8.GetByteCount(value);

            Array.Clear(_writeBuffer, 0, headerSize);
            _writeBuffer[0] = (byte)MagicBytes.ResponsePacket;
            _writeBuffer[1] = (byte)Opcode.Get;
            
            const int extrasLength = 0x04;
            _writeBuffer[4] = extrasLength;

            int totalBodyLength = valueLength + extrasLength;

            _writeBuffer[8] = (byte)(totalBodyLength >> 24);
            _writeBuffer[9] = (byte)(totalBodyLength >> 16);
            _writeBuffer[10] = (byte)(totalBodyLength >> 8);
            _writeBuffer[11] = (byte)(totalBodyLength);

            _writeBuffer[12] = (byte)(opaque >> 24);
            _writeBuffer[13] = (byte)(opaque >> 16);
            _writeBuffer[14] = (byte)(opaque >> 8);
            _writeBuffer[15] = (byte)(opaque);

            _writeBuffer[24] = 0xde;
            _writeBuffer[25] = 0xad;
            _writeBuffer[26] = 0xbe;
            _writeBuffer[27] = 0xef;

            int valueOffset = headerSize + extrasLength;
            Encoding.UTF8.GetBytes(value, 0, value.Length, _writeBuffer, valueOffset);
            
            //Could also make this async, but who cares... again... test code.
            _stream.Write(_writeBuffer, 0, totalBodyLength + headerSize);
        }

        private void WriteNotMyVBucket(int opaque)
        {
            var errorMessage = "Vbucket Elsewhere";
            var valueLength = Encoding.UTF8.GetByteCount(errorMessage);

            Array.Clear(_writeBuffer, 0, headerSize);

            _writeBuffer[0] = (byte)MagicBytes.ResponsePacket;
            _writeBuffer[1] = (byte)Opcode.Get;

            _writeBuffer[6] = (byte)(((uint)ResponseStatus.VbucketBelongsToAnotherServer) >> 8);
            _writeBuffer[7] = (byte)(ResponseStatus.VbucketBelongsToAnotherServer);

            int totalBodyLength = valueLength;

            _writeBuffer[8] = (byte)(totalBodyLength >> 24);
            _writeBuffer[9] = (byte)(totalBodyLength >> 16);
            _writeBuffer[10] = (byte)(totalBodyLength >> 8);
            _writeBuffer[11] = (byte)(totalBodyLength);

            _writeBuffer[12] = (byte)(opaque >> 24);
            _writeBuffer[13] = (byte)(opaque >> 16);
            _writeBuffer[14] = (byte)(opaque >> 8);
            _writeBuffer[15] = (byte)(opaque);

            Encoding.UTF8.GetBytes(errorMessage, 0, errorMessage.Length, _writeBuffer, headerSize);

            //Could also make this async, but who cares... again... test code.
            _stream.Write(_writeBuffer, 0, totalBodyLength + headerSize);
        }

        private Stream OnRead(IAsyncResult result)
        {
            int bytesRead = _stream.EndRead(result);
            if (bytesRead == 0)
            {
                return _readAsync.Stop();
            }
            _currentByteInReadBuffer += bytesRead;

            bool shouldKeepEmptyingBuffer;
            do
            {
                shouldKeepEmptyingBuffer = false;
                int totalHeaderSize = headerSize;
                if (_currentByteInReadBuffer >= totalHeaderSize)
                {
                    const int totalBodyLengthFieldOffset = 8;
                    var totalBodyLength = BitParser.ParseInt(_readBuffer, totalBodyLengthFieldOffset);

                    int totalBytesInRequest = totalBodyLength + totalHeaderSize;
                    if (_currentByteInReadBuffer >= totalBytesInRequest)
                    {
                        const int vBucketFieldOffset = 6;
                        var vBucket = BitParser.ParseUShort(_readBuffer, vBucketFieldOffset);

                        const int opaqueFieldOffset = 12;
                        var opaque = BitParser.ParseInt(_readBuffer, opaqueFieldOffset);

                        var bytesLeftOver = _currentByteInReadBuffer - totalBytesInRequest;

                        //Yeah this is cheesy, but come on... This is test code... 
                        Array.Copy(_readBuffer, totalBytesInRequest, _readBuffer, 0, bytesLeftOver);
                        _currentByteInReadBuffer -= totalBytesInRequest;

                        bool isMyVbucket;
                        lock (_gate)
                        {
                            isMyVbucket = _vBuckets.Contains(vBucket);
                        }
                        
                        if (isMyVbucket)
                        {
                            WriteGetResponse(opaque, "{\"SomeValue\":121}");
                        }
                        else
                        {
                            WriteNotMyVBucket(opaque);
                        }

                        shouldKeepEmptyingBuffer = true;
                    }
                }
            } while (shouldKeepEmptyingBuffer);

            return _readAsync.Continue(_stream);
        }

        private Stream OnReadError(IAsyncResult result, Exception e)
        {
            Console.WriteLine("MemcachedServer disconnected: " + e.ToString());

            return _readAsync.Stop();
        }

        public void Dispose()
        {
            try
            {
                if (_stream != null)
                {
                    _stream.Close();
                }
            }
            catch
            { }

            try
            {
                if (_tcpClient != null)
                {
                    _tcpClient.Close();
                }
            }
            catch
            { }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public class StringDecoder : IDisposable
    {
        private static BufferPool<char> BufferPool = new BufferPool<char>(256, 1024);
        
        private StringBuilder _builder;
        private Decoder _decoder ;
        private ArraySegment<char> _decodeBuffer;
        
        public StringDecoder()
        {
            _builder = new StringBuilder();
            _decoder = Encoding.UTF8.GetDecoder();
            _decodeBuffer = BufferPool.Get();
        }

        public void Decode(ArraySegment<byte> sourceBuffer)
        {
            if (sourceBuffer.Count == 0)
                return;

            unsafe
            {
                fixed (byte* pSource = sourceBuffer.Array)
                fixed (char* pDecode = _decodeBuffer.Array)
                {
                    int bytesUsed;
                    int charsUsed;
                    bool completed;
                    
                    _decoder.Convert(pSource + sourceBuffer.Offset, sourceBuffer.Count, pDecode + _decodeBuffer.Offset, _decodeBuffer.Count, false, out bytesUsed, out charsUsed, out completed);
                    
                    _builder.Append(_decodeBuffer.Array, _decodeBuffer.Offset, charsUsed);
                }
            }
        }

        public void Dispose()
        {
            BufferPool.Return(_decodeBuffer);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}

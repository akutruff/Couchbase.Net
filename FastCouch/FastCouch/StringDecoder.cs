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
        private Decoder _decoder;
        private ArraySegment<char> _decodeBuffer;
        private int _previousByte = 0;
        private bool _hasBeenDisposed = false;

        public StringDecoder()
        {
            _builder = new StringBuilder();
            _decoder = Encoding.UTF8.GetDecoder();
            _decodeBuffer = BufferPool.Get();
        }

        public StringDecoder(ArraySegment<char> decodeBuffer)
        {
            _builder = new StringBuilder();
            _decoder = Encoding.UTF8.GetDecoder();
            _decodeBuffer = decodeBuffer;
        }

        public int Decode(ArraySegment<byte> sourceBuffer)
        {
            if (sourceBuffer.Count == 0)
                return 0;

            unsafe
            {
                fixed (byte* pSource = sourceBuffer.Array)
                fixed (char* pDecode = _decodeBuffer.Array)
                {
                    return Decode(pSource + sourceBuffer.Offset, sourceBuffer.Count, pDecode + _decodeBuffer.Offset, _decodeBuffer.Count);
                }
            }
        }
        private unsafe int Decode(byte* pSource, int sourceCount, char* pDecode, int decodeCount)
        {
            int totalBytesDecoded = 0;
            int startingLength = _builder.Length;

            while (totalBytesDecoded < sourceCount)
            {
                int bytesUsed;
                int charsUsed;
                bool completed;

                _decoder.Convert(pSource + totalBytesDecoded, sourceCount - totalBytesDecoded, pDecode, decodeCount, false, out bytesUsed, out charsUsed, out completed);

                _builder.Append(_decodeBuffer.Array, _decodeBuffer.Offset, charsUsed);
                totalBytesDecoded += bytesUsed;
            }

            var totalCharactersAdded = _builder.Length - startingLength;
            return totalCharactersAdded;
        }

        public bool DecodeAndSplitAtUtf8Character(ArraySegment<byte> sourceBuffer, char characterToResetDecoding, out string stringUpToCharacter, out ArraySegment<byte> bytesLeftover)
        {
            if ((characterToResetDecoding & 0xFF00) != 0)
            {
                throw new Exception("Only UTF8 representable characters are allowed");
            }

            if (sourceBuffer.Count > 0)
            {
                unsafe
                {
                    fixed (byte* pSourceArray = sourceBuffer.Array)
                    fixed (char* pDecodeArray = _decodeBuffer.Array)
                    {

                        byte* pSource = pSourceArray + sourceBuffer.Offset;
                        char* pDecode = pDecodeArray + _decodeBuffer.Offset;

                        byte charAsByte = (byte)characterToResetDecoding;

                        for (int i = 0; i < sourceBuffer.Count; i++)
                        {
                            int currentByte = *(pSource + i);

                            if (currentByte == charAsByte && (_previousByte & 0xFF00) == 0)
                            {
                                int numberOfBytesToDecode = i;
                                Decode(pSource, numberOfBytesToDecode, pDecode, _decodeBuffer.Count);

                                stringUpToCharacter = _builder.ToString();

                                int bytesForRestOfStringExcludingTheSpecialCharacter = Math.Max(sourceBuffer.Count - numberOfBytesToDecode - 1, 0);

                                int indexOfByteImmediatelyFollowingTheSpecialCharacter = sourceBuffer.Offset + Math.Min(numberOfBytesToDecode + 1, sourceBuffer.Count);

                                bytesLeftover = new ArraySegment<byte>(sourceBuffer.Array, indexOfByteImmediatelyFollowingTheSpecialCharacter, bytesForRestOfStringExcludingTheSpecialCharacter);

                                _builder = new StringBuilder();
                                _previousByte = 0;

                                return true;
                            }

                            _previousByte = currentByte;
                        }

                        Decode(pSource, sourceBuffer.Count, pDecode, _decodeBuffer.Count);
                    }
                }
            }

            stringUpToCharacter = null;
            bytesLeftover = new ArraySegment<byte>();
            return false;
        }

        public void Dispose()
        {
            if (_hasBeenDisposed)
            {
                throw new Exception();
            }
            BufferPool.Return(_decodeBuffer);
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}

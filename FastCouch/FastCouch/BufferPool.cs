using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public class BufferPool<T>
    {
        private readonly T[] _masterBuffer;
        
        private readonly int[] _freeBufferIndices;
        private readonly int _bufferSize;
        private readonly int _numberOfBuffers;

        private readonly object _gate = new object();
        
        private int _freeBufferCount;

        public BufferPool(int numberOfBuffers, int bufferSize)
        {
            _numberOfBuffers = numberOfBuffers;
            _bufferSize = bufferSize;

            int totalNumberOfBytesInBufferPool = numberOfBuffers * bufferSize;
            _masterBuffer = new T[totalNumberOfBytesInBufferPool];
        
            _freeBufferCount = numberOfBuffers;

            _freeBufferIndices = new int[numberOfBuffers];

            int bufferOffset = 0;
            for (int i = 0; i < numberOfBuffers; i++)
            {
                _freeBufferIndices[i] = bufferOffset;
                bufferOffset += bufferSize;
            }
        }

        public ArraySegment<T> Get()
        {
            return Get(_bufferSize);
        }

        public ArraySegment<T> Get(int numberOfBytes)
        {
            if (numberOfBytes <= _bufferSize)
            {
                int freeBufferIndex = -1;
                lock (_gate)
                {
                    if (_freeBufferCount > 0)
                    {
                        freeBufferIndex = _freeBufferIndices[--_freeBufferCount];
                    }
                }

                if (freeBufferIndex >= 0)
                {
                    return new ArraySegment<T>(_masterBuffer, freeBufferIndex, numberOfBytes);
                }
            }
            
            return new ArraySegment<T>(new T[_bufferSize]);
        }


        public void Return(ArraySegment<T> buffer)
        {
            if (buffer.Array != _masterBuffer)
                return;

            int freeBufferIndex;
            lock (_gate)
            {
                freeBufferIndex = _freeBufferCount++;
            }

            _freeBufferIndices[freeBufferIndex] = buffer.Offset;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace FastCouch.Tests
{
    public class TestStream : Stream
    {
        Action<byte[], int, int> OnRead;
        Action<byte[], int, int> OnWritten;

        public TestStream(Action<byte[], int, int> onRead, Action<byte[], int, int> onWritten)
        {
            OnRead = onRead;
            OnWritten = onWritten;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            OnRead(buffer, offset, count);
            return count;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            OnWritten(buffer, offset, count);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }


        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public class SetCommand : MemcachedCommand
    {
        public override Opcode Opcode { get { return FastCouch.Opcode.Set; } }
        public string Value { get; private set; }

        Encoder _encoder;
        private int _currentCharacter;

        public SetCommand(int id, string key, string value, long cas, object state, Action<ResponseStatus, string, long, object> onComplete)
            : base(id, state, key, onComplete)
        {
            Value = value;

            this.RequestHeader.ExtrasLength = 8;
            this.RequestHeader.TotalBodyLength += this.RequestHeader.ExtrasLength; 
            
            this.RequestHeader.TotalBodyLength += Encoding.UTF8.GetByteCount(Value);
            this.RequestHeader.Cas = cas;
        }

        public override void BeginWriting()
        {
            //Encoder is stateful according to the docs...
            _encoder = Encoding.UTF8.GetEncoder();
            _currentCharacter = 0;
        }

        public override int WriteValue(ArraySegment<byte> bodyBuffer, int currentByteInBody)
        {
            //Need at least a two byte buffer in case of Unicode 16
            if (bodyBuffer.Count < 2)
                return 0;
                        
            unsafe
            {
                fixed (char* pChars = this.Value)
                fixed (byte* pBodyBuffer = bodyBuffer.Array)
                {
                    int charactersToWrite = this.Value.Length - _currentCharacter;

                    int charsUsed;
                    int bytesWritten;
                    bool completed;

                    _encoder.Convert(pChars + _currentCharacter, charactersToWrite, pBodyBuffer + bodyBuffer.Offset, bodyBuffer.Count, false, out charsUsed, out bytesWritten, out completed);

                    _currentCharacter += charsUsed;

                    return bytesWritten;
                }
            }
        }

        public override void NotifyComplete()
        {
            OnComplete(ResponseStatus, string.Empty, this.Cas, this.State);
        }
    }
}
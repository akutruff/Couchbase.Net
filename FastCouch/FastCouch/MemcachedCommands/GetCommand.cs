using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public class GetCommand : MemcachedCommand
    {
        public override Opcode Opcode { get { return FastCouch.Opcode.Get; } }
        private StringDecoder _decoder;

        public GetCommand(int id, string key, object state, Action<ResponseStatus, string, long, object> onComplete)
            : base(id, state, key, onComplete)
        {
        }

        public override void Parse(
            ResponseStatus responseStatus,
            ArraySegment<byte> bodyData,
            ArraySegment<byte> extras,
            ArraySegment<byte> key,
            ArraySegment<char> encodingBuffer,
            int bytesOfBodyPreviouslyRead,
            int totalBodyLength)
        {
            if (bytesOfBodyPreviouslyRead == 0)
            {
                _decoder = new StringDecoder();
            }

            _decoder.Decode(bodyData);
        }

        public override void NotifyComplete()
        {
            var value = string.Empty;
            if (_decoder != null)
            {
                value = _decoder.ToString();
                _decoder.Dispose();
            }
            OnComplete(ResponseStatus, value, this.Cas, this.State);
        }
    }
}
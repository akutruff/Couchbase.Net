using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public class LineReadingHttpCommand : HttpCommand
    {
        Func<string, object, bool> _onLineReadAndShouldContinue;

        public LineReadingHttpCommand(UriBuilder builder, object state, Func<string, object, bool> onLineReadAndShouldContinue, Action<ResponseStatus, string, object> onComplete)
            : base(builder, state, onComplete)
        {
            _onLineReadAndShouldContinue = onLineReadAndShouldContinue;
        }

        public override bool OnRead(int bytesRead)
        {
            var buffer = HttpReadState.Buffer;
            var dataRead = new ArraySegment<byte>(buffer.Array, buffer.Offset, bytesRead);
            
            string value;
            while (HttpReadState.StringDecoder.DecodeAndSplitAtUtf8Character(dataRead, '\n', out value, out dataRead))
            {
                if (!_onLineReadAndShouldContinue(value, this.State))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

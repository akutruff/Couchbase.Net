using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public class QuitCommand : MemcachedCommand
    {
        public override Opcode Opcode { get { return FastCouch.Opcode.Quit; } }

        public QuitCommand(int id, Action<ResponseStatus, string, long, object> onComplete)
            : base(id, null, string.Empty, onComplete)
        {
        }
    }
}

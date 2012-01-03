using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public class DeleteCommand : MemcachedCommand
    {
        public override Opcode Opcode { get { return FastCouch.Opcode.Delete; } }

        public DeleteCommand(int id, string key, object state, Action<ResponseStatus, string, long, object> onComplete)
            : base(id, state, key, onComplete)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public struct MemcachedHeader
    {
        public string Key;
        public long Cas;
        public byte ExtrasLength;
        public int KeyLength;
        public int TotalBodyLength;
        public int Opaque;
        public int VBucket;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Json;

namespace FastCouch
{
    public static class VBucketCalculator
    {
        public static int GetId(string key, int vBucketCount)
        {
            var crcOfKey = (int)Crc32.GetUTF8HashOptimisticallyAssumingItIsUtf8(key);
            
            int vbucketId = crcOfKey >> 16 & vBucketCount - 1;  //Slick ass code taken from the official python client on github.
            
            return vbucketId;
        }
    }
}

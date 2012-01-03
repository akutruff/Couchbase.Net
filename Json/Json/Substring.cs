using System;
using System.Collections.Generic;

namespace Json
{
    public struct Substring
    {
        public readonly string String;
        public readonly int Index;
        public readonly int Length;

        public Substring(string sourceString, int index, int length)
        {
            String = sourceString;
            Index = index;
            Length = length;
        }

        public Substring(string sourceString)
            : this(sourceString, 0, sourceString.Length)
        {
        }

        public override string ToString()
        {
            return (Index == 0 && String.Length == Length) ? String : String.Substring(Index, Length);
        }

        public HashedSubstring GetHashedSubstring()
        {
            return new HashedSubstring(String, Index, Length);
        }
    }

    public struct HashedSubstring
    {
        public readonly string String;
        public readonly int Index;
        public readonly int Length;
        public readonly int HashCode;

        public HashedSubstring(string sourceString, int index, int length)
        {
            String = sourceString;
            Index = index;
            Length = length;
            HashCode = CalculateHashCode(sourceString, index, length);
        }

        public HashedSubstring(string sourceString)
            : this(sourceString, 0, sourceString.Length)
        {
        }

        private static int CalculateHashCode(string sourceString, int index, int length)
        {
            return (int)Crc32.GetUTF8HashOptimisticallyAssumingItIsSingleByteUtf16(sourceString, index, length);
        }

        public override string ToString()
        {
            return (Index == 0 && String.Length == Length) ? String : String.Substring(Index, Length);
        }

        public override int GetHashCode()
        {
            return this.HashCode;
        }
    }
}

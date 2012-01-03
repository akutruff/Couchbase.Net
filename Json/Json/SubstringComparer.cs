using System;
using System.Collections.Generic;

namespace Json
{
    public class HashedSubstringComparer : IEqualityComparer<HashedSubstring>
    {
        public static readonly HashedSubstringComparer Default = new HashedSubstringComparer();

        private HashedSubstringComparer()
        {
        }

        public bool Equals(HashedSubstring x, HashedSubstring y)
        {
            return x.Length == y.Length && x.HashCode == y.HashCode && string.Compare(x.String, x.Index, y.String, y.Index, x.Length) == 0;
        }

        public int GetHashCode(HashedSubstring obj)
        {
            return obj.HashCode;
        }
    }
}

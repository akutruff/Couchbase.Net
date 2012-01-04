using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    internal static class MathUtils
    {
        public static int CircularIncrement(int value, int maxValue)
        {
            return (value + 1) % maxValue;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public static class BufferUtils
    {
        public static int CalculateMaxPossibleBytesForCopy(int arrayOneIndex, int arrayOneLength, int arrayTwoIndex, int arrayTwoLength)
        {
            var bytesLeftInArrayOne = arrayOneLength - arrayOneIndex;
            var bytesLeftInArrayTwo = arrayTwoLength - arrayTwoIndex;

            int bytesAvailable = Math.Min(bytesLeftInArrayOne, bytesLeftInArrayTwo);
            
            return bytesAvailable;
        }
        
        public static int CopyAsMuchAsPossible(byte[] sourceArray, ref int sourceIndex, int sourceArrayLength, byte[] destinationArray, ref int destinationIndex, int destinationArrayLength)
        {
            if (sourceArrayLength == 0 || destinationArrayLength == 0)
            {
                return 0;
            }

            var bytesAvailable = CalculateMaxPossibleBytesForCopy(sourceIndex, sourceArrayLength, destinationIndex, destinationArrayLength);

            Array.Copy(sourceArray, sourceIndex, destinationArray, destinationIndex, bytesAvailable);

            sourceIndex += bytesAvailable;
            destinationIndex += bytesAvailable;

            return bytesAvailable;
        }
    }
}

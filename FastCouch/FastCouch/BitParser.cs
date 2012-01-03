using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public static class BitParser
    {
        public static int ParseInt(byte[] buffer, int firstByteOfValueInBuffer)
        {
            return
                buffer[firstByteOfValueInBuffer] << 24 |
                buffer[firstByteOfValueInBuffer + 1] << 16 |
                buffer[firstByteOfValueInBuffer + 2] << 8 |
                buffer[firstByteOfValueInBuffer + 3];
        }

        public static long ParseLong(byte[] buffer, int firstByteOfValueInBuffer)
        {
            //Done it two parts to prevent lots of casting along the way which would be bad on a 32 bit system.
            long upper = ParseInt(buffer, firstByteOfValueInBuffer);
            long lower = ParseInt(buffer, firstByteOfValueInBuffer + 4);

            return
                upper << 32 |
                lower;
        }

        public static int ParseUShort(byte[] buffer, int firstByteOfValueInBuffer)
        {
            return
                buffer[firstByteOfValueInBuffer] << 8 |
                buffer[firstByteOfValueInBuffer + 1];
        }

        public static ResponseStatus ParseResponseStatus(byte[] buffer, int firstByteOfValueInBuffer)
        {
            return (ResponseStatus)(buffer[firstByteOfValueInBuffer] << 8 | buffer[firstByteOfValueInBuffer + 1]);
        }
    }
}

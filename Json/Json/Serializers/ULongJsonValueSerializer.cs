using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class ULongJsonValueSerializer : IJsonValueParser<ulong>, IJsonValueWriter<ulong>
    {
        public ulong Parse(string json, ref int index)
        {
            var character = json[index++];

            if (character < '0' || character > '9')
            {
                throw new ArgumentOutOfRangeException("Not a number");
            }
                
            ulong value = (ulong)(character - '0');

            while (index < json.Length)
            {
                character = json[index];
                if (character < '0' || character > '9')
                {
                    break;
                }

                var digit = (ulong)(character - '0');

                value *= 10;

                value += digit;
                index++;
            }

            return value;
        }

        public void Append(StringBuilder builder, ulong value)
        {
            builder.Append(value);
        }
    }
}
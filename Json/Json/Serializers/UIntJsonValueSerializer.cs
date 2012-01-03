using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class UIntJsonValueSerializer : IJsonValueParser<uint>, IJsonValueWriter<uint>
    {
        public uint Parse(string json, ref int index)
        {
            var character = json[index++];

            if (character < '0' || character > '9')
            {
                throw new ArgumentOutOfRangeException("Not a number");
            }

            uint value = (uint)(character - '0');

            while (index < json.Length)
            {
                character = json[index];
                if (character < '0' || character > '9')
                {
                    break;
                }

                var digit = (uint)(character - '0');

                value *= 10;

                value += digit;
                index++;
            }

            return value;
        }

        public void Append(StringBuilder builder, uint value)
        {
            builder.Append(value);
        }
    }
}

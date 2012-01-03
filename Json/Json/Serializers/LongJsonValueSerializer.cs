using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class LongJsonValueSerializer : IJsonValueParser<long>, IJsonValueWriter<long>
    {
        public long Parse(string json, ref int index)
        {
            char character = json[index];

            bool isPositive = true;
            if (character == '-')
            {
                isPositive = false;
                index++;
            }
            else if (character == '+')
            {
                index++;
            }

            const int zeroCharacter = '0';

            character = json[index++];

            if (character < '0' || character > '9')
            {
                throw new ArgumentOutOfRangeException("Not a number");
            }

            long value = character - zeroCharacter;

            while (index < json.Length)
            {
                character = json[index];
                if (character < '0' || character > '9')
                {
                    break;
                }

                long digit = character - '0';

                value *= 10;

                value += digit;
                index++;
            }

            return isPositive ? value : -value;
        }

        public void Append(StringBuilder builder, long value)
        {
            builder.Append(value);
        }
    }
}

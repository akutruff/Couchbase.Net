using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class IntJsonValueSerializer : IJsonValueParser<int>, IJsonValueWriter<int>
    {
        public int Parse(string json, ref int index)
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

            int value = character - zeroCharacter;

            for (; index < json.Length; index++)
            {
                character = json[index];
                if (character < '0' || character > '9')
                {
                    break;
                }

                var digit = character - '0';

                value *= 10;

                value += digit;
            }

            return isPositive ? value : -value;
        }

        public void Append(StringBuilder builder, int value)
        {
            builder.Append(value);
        }
    }
}
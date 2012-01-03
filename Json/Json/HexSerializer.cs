using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public static class HexSerializer 
    {
        public static uint Parse(string json, ref int index, int charactersForHexValue)
        {
            char character = json[index++];

            uint value = GetCharacterHexValue(character);
            for (int i = 1; i < charactersForHexValue; i++)
            {
                character = json[index];
                var hexCharacterValue = GetCharacterHexValue(character);

                value <<= 4;

                value += hexCharacterValue;
                index++;
            }

            return value;
        }

        public static uint GetCharacterHexValue(char character)
        {
            int value;
            if (character <= '9' && character >= '0')
            {
                value = character - '0';
            }
            else if (character <= 'f' && character >= 'a')
            {
                value = character - 'a' + 10;
            }
            else if (character <= 'F' && character >= 'A')
            {
                value = character - 'A' + 10;
            }
            else
            {
                throw new ArgumentOutOfRangeException("Not a valid hex character: " + character);
            }

            return (uint)value;
        }

        public static void WriteHexEncoded(StringBuilder builder, char character)
        {
            builder.Append(GetCharacterForNibble(character >> 12));
            builder.Append(GetCharacterForNibble(character >> 8));
            builder.Append(GetCharacterForNibble(character >> 4));
            builder.Append(GetCharacterForNibble(character));
        }

        private static char GetCharacterForNibble(int nibble)
        {
            byte casted = (byte)(nibble & 0xF);

            if (casted < 10)
            {
            	return (char)('0' + casted);
            }
            else
            {
                return (char)('a' - 10 + casted);                
            }
        }
    }
}

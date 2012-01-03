using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class GuidJsonValueSerializer : IJsonValueParser<Guid>, IJsonValueWriter<Guid>
    {
        //private static byte[][] HexPremultiplyTable;

        //static GuidJsonValueParser()
        //{
        //    const int MaxHexValue = 16;

        //    HexPremultiplyTable = new byte[MaxHexValue][];

        //    for (int i = 0; i < MaxHexValue; i++)
        //    {
        //        byte[] hexValues = new byte[MaxHexValue];
        //        HexPremultiplyTable[i] = hexValues;

        //        for (int j = 0; j < MaxHexValue; j++)
        //        {
        //            hexValues[j] = (byte)(MaxHexValue * i + j);
        //        }
        //    }
        //}

        public Guid Parse(string json, ref int index)
        {
            if (json[index] != '\"')
            {
                throw new Exception("Guids must be inside a string");
            }
            index++;

            int a = GetHexInt(json, ref index);
            
            if (json[index] == '-')
                index++;

            short b = GetHexShort(json, ref index);

            if (json[index] == '-')
                index++;

            short c = GetHexShort(json, ref index);

            if (json[index] == '-')
                index++;

            byte d = GetHexByte(json, ref index);
            byte e = GetHexByte(json, ref index);

            if (json[index] == '-')
                index++;

            byte f = GetHexByte(json, ref index);
            byte g = GetHexByte(json, ref index);
            byte h = GetHexByte(json, ref index);
            byte i = GetHexByte(json, ref index);
            byte j = GetHexByte(json, ref index);
            byte k = GetHexByte(json, ref index);

            if (json[index] != '\"')
            {
                throw new Exception("Guids must be inside a string with nothing else in it");
            }
            index++;

            var guid = new Guid(a, b, c, d, e, f, g, h, i, j, k);
                            
            return guid;
        }

        private static byte GetHexByte(string json, ref int index)
        {
            var result =
                HexSerializer.GetCharacterHexValue(json[index++]) << 4 |
                HexSerializer.GetCharacterHexValue(json[index++]);

            return (byte)result;
        }

        private static int GetHexInt(string json, ref int index)
        {
            var result =
                HexSerializer.GetCharacterHexValue(json[index++]) << 28 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 24 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 20 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 16 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 12 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 8 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 4 |
                HexSerializer.GetCharacterHexValue(json[index++]);

            return (int)result;
        }

        private static short GetHexShort(string json, ref int index)
        {
            var result =
                HexSerializer.GetCharacterHexValue(json[index++]) << 12 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 8 |
                HexSerializer.GetCharacterHexValue(json[index++]) << 4 |
                HexSerializer.GetCharacterHexValue(json[index++]);

            return (short)result;
        }

        public void Append(StringBuilder builder, Guid value)
        {
            builder.Append('\"');
            builder.Append(value.ToString());
            builder.Append('\"');
        }
    }
}

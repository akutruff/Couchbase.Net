using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class StringJsonValueSerializer : IJsonValueParser<string>, IJsonValueWriter<string>
    {
        public string Parse(string json, ref int index)
        {
            Substring substring;
            Scanner.GetNextString(json, ref index, out substring);

            return substring.ToString();
        }

        public void Append(StringBuilder builder, string value)
        {
            builder.Append('\"');

            for (int i = 0; i < value.Length; i++)
            {
                var character = value[i];

                if ((character & 0xFF00) == 0)
                {
                    switch (character)
                    {
                        case '\'':
                            builder.Append(@"\'");
                            break;
                        case '\"':
                            builder.Append(@"\""");
                            break;
                        case '\\':
                            builder.Append(@"\\");
                            break;
                        case '/':
                            builder.Append(@"\/");
                            break;
                        case '\n':
                            builder.Append(@"\n");
                            break;
                        case '\r':
                            builder.Append(@"\r");
                            break;
                        case '\f':
                            builder.Append(@"\f");
                            break;
                        case '\b':
                            builder.Append(@"\b");
                            break;
                        case '\t':
                            builder.Append(@"\t");
                            break;
                        default:
                            builder.Append(character);
                            break;
                    }
                }
                else
                {
                    builder.Append(@"\u");
                    HexSerializer.WriteHexEncoded(builder, character);
                }
            }

            builder.Append('\"');
        }
    }
}
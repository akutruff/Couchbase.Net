using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class BoolJsonValueSerializer : IJsonValueParser<bool>, IJsonValueWriter<bool>
    {
        public bool Parse(string json, ref int index)
        {
            var character = json[index];
            switch (character)
            {
                case 't':
                    if (Scanner.SkipWord(json, ref index, "true"))
                    {
                        return true;
                    }
                    throw new Exception("Not a bool");
                case 'f':
                    if (Scanner.SkipWord(json, ref index, "false"))
                    {
                        return false;
                    }
                    throw new Exception("Not a bool");
                default:
                    throw new Exception("Not a bool");
            }
        }

        public void Append(StringBuilder builder, bool value)
        {
            builder.Append(value ? "true" : "false");
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class FloatJsonValueSerializer : IJsonValueParser<float>, IJsonValueWriter<float>
    {
        public float Parse(string json, ref int index)
        {
            int length;
            int startingIndex;

            Scanner.ScanPastDouble(json, ref index, out startingIndex, out length);

            var value = float.Parse(json.Substring(startingIndex, length));

            return value;
        }

        public void Append(StringBuilder builder, float value)
        {
            builder.Append(value);
        }
    }
}

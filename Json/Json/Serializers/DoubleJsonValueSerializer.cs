using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class DoubleJsonValueSerializer : IJsonValueParser<double>, IJsonValueWriter<double>
    {
        public double Parse(string json, ref int index)
        {
            int length;
            int startingIndex;
            
            Scanner.ScanPastDouble(json, ref index, out startingIndex, out length);
            
            var value = double.Parse(json.Substring(startingIndex, length));
            
            return value;
        }

        public void Append(StringBuilder builder, double value)
        {
            builder.Append(value);
        }
    }
}
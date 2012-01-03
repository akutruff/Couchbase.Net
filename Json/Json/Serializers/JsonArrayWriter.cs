using System;
using System.Collections.Generic;
using System.Text;

namespace Json
{
    public class JsonArrayWriter<TElement> : IJsonValueWriter<List<TElement>>
    {
        private readonly IJsonValueWriter<TElement> _elementWriter;

        public JsonArrayWriter(IJsonValueWriter<TElement> elementWriter)
        {
            _elementWriter = elementWriter;
        }

        public void Append(StringBuilder builder, List<TElement> value)
        {
            builder.Append('[');
            
            if (value.Count > 0)
            {
                int countExcludingLast = value.Count - 1;

                int i = 0;
                for (; i < countExcludingLast; i++)
                {
                    var element = value[i];
                    _elementWriter.Append(builder, element);
                    builder.Append(',');
                }

                _elementWriter.Append(builder, value[i]);
            }

            builder.Append(']');
        }
    }
}

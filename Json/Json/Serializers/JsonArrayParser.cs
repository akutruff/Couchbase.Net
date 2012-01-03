using System;
using System.Collections.Generic;

namespace Json
{
    public class JsonArrayParser<TElement> : IJsonValueParser<List<TElement>>
    {
        private readonly IJsonValueParser<TElement> _elementParser;

        public JsonArrayParser(IJsonValueParser<TElement> elementParser)
        {
            _elementParser = elementParser;
        }

        public List<TElement> Parse(string json, ref int index)
        {
            var character = json[index];
            if (character != '[')
            {
                throw new Exception("This is not an array as expected");
            }
            
            index++;

            List<TElement> elements = new List<TElement>();

            while (index < json.Length)
            {
                Scanner.SkipWhitespace(json, ref index);

                character = json[index];

                if (character == ']')
                {
                    index++;
                    return elements;
                }
                else
                {
                    var value = _elementParser.Parse(json, ref index);
                    elements.Add(value);

                    Scanner.SkipWhitespace(json, ref index);
                    if (json[index] == ',')
                    {
                    	index++;
                    }
                }
            }

            return elements;
        }
    }
}
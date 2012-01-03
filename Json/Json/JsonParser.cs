using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;

namespace Json
{
    public static class JsonParser
    {
        public static readonly IJsonValueParser<string> StringParser = new StringJsonValueSerializer();
        public static readonly IJsonValueParser<int> IntParser = new IntJsonValueSerializer();
        public static readonly IJsonValueParser<uint> UIntParser = new UIntJsonValueSerializer();
        public static readonly IJsonValueParser<bool> BoolParser = new BoolJsonValueSerializer();
        public static readonly IJsonValueParser<double> DoubleParser = new DoubleJsonValueSerializer();
        public static readonly IJsonValueParser<float> FloatParser = new FloatJsonValueSerializer();
        public static readonly IJsonValueParser<long> LongParser = new LongJsonValueSerializer();
        public static readonly IJsonValueParser<ulong> ULongParser = new ULongJsonValueSerializer();
        public static readonly IJsonValueParser<Guid> GuidParser = new GuidJsonValueSerializer();

        public static IJsonParsingOperations<T> Create<T>()
        {
            return new JsonGroupParser<T>();
        }
    }
}
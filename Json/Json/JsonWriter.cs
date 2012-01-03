using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public class JsonWriter
    {
        public static readonly IJsonValueWriter<string> StringWriter = new StringJsonValueSerializer();
        public static readonly IJsonValueWriter<int> IntWriter = new IntJsonValueSerializer();
        public static readonly IJsonValueWriter<uint> UIntWriter = new UIntJsonValueSerializer();
        public static readonly IJsonValueWriter<bool> BoolWriter = new BoolJsonValueSerializer();
        public static readonly IJsonValueWriter<double> DoubleWriter = new DoubleJsonValueSerializer();
        public static readonly IJsonValueWriter<float> FloatWriter = new FloatJsonValueSerializer();
        public static readonly IJsonValueWriter<long> LongWriter = new LongJsonValueSerializer();
        public static readonly IJsonValueWriter<ulong> ULongWriter = new ULongJsonValueSerializer();
        public static readonly IJsonValueWriter<Guid> GuidWriter = new GuidJsonValueSerializer();

        public static IJsonWritingOperations<T> Create<T>()
        {
            return new JsonGroupWriter<T>();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Json
{
    public interface IJsonGroupWriter<T> : IJsonValueWriter<T>
    {
        string ToString(T value);
    }

    public interface IJsonValueWriter<TValue>
    {
        void Append(StringBuilder builder, TValue value);
    }

    public interface IJsonWritingOperations<T> : IJsonGroupWriter<T>
    {
        IJsonWritingOperations<T> Write<TSubGroup>(string fieldName, Func<T, TSubGroup> valueGetter, IJsonGroupWriter<TSubGroup> subGroupParser);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, Guid> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, string> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, int> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, uint> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, long> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, ulong> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, double> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, float> valueGetter);
        IJsonWritingOperations<T> Write(string fieldName, Func<T, bool> valueGetter);
        IJsonWritingOperations<T> Write<TElement>(string fieldName, Func<T, List<TElement>> valueGetter, IJsonValueWriter<TElement> elementParser);
    }

    public class JsonGroupWriter<T> : IJsonWritingOperations<T>
    {
        private List<IJsonFieldWritingInstruction> _fieldWritingInstructions = new List<IJsonFieldWritingInstruction>();

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, string> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.StringWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, int> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.IntWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, bool> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.BoolWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write<TElement>(string fieldName, Func<T, List<TElement>> valueGetter, IJsonValueWriter<TElement> elementWriter)
        {
            AddInstruction(fieldName, new JsonArrayWriter<TElement>(elementWriter), valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, uint> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.UIntWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, long> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.LongWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, ulong> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.ULongWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, double> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.DoubleWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, float> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.FloatWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write(string fieldName, Func<T, Guid> valueGetter)
        {
            AddInstruction(fieldName, JsonWriter.GuidWriter, valueGetter);
            return this;
        }

        public IJsonWritingOperations<T> Write<TSubGroup>(string fieldName, Func<T, TSubGroup> valueGetter, IJsonGroupWriter<TSubGroup> subGroupWriter)
        {
            AddInstruction(fieldName, subGroupWriter, valueGetter);
            return this;
        }

        public void AddInstruction<TValue>(string fieldName, IJsonValueWriter<TValue> jsonValueWriter, Func<T, TValue> valueGetter)
        {
            _fieldWritingInstructions.Add(new JsonFieldWritingInstruction<TValue>(fieldName, valueGetter, jsonValueWriter));
        }

        private interface IJsonFieldWritingInstruction
        {
            void Write(StringBuilder builder, T target);
        }

        public string ToString(T value)
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, value);
            return builder.ToString();
        }

        public void Append(StringBuilder builder, T value)
        {
            builder.Append('{');
            if (_fieldWritingInstructions.Count > 0)
            {
                var lastIndex = _fieldWritingInstructions.Count - 1;
                int i = 0;
                for (; i < lastIndex; i++)
                {
                    _fieldWritingInstructions[i].Write(builder, value);
                    builder.Append(',');
                }
                _fieldWritingInstructions[i].Write(builder, value);
            }
            builder.Append('}');
        }

        private class JsonFieldWritingInstruction<TValue> : IJsonFieldWritingInstruction
        {
            public string _fieldName;
            private IJsonValueWriter<TValue> _valueWriter;
            public Func<T, TValue> _valueGetter;

            public JsonFieldWritingInstruction(string fieldName, Func<T, TValue> valueGetter, IJsonValueWriter<TValue> valueWriter)
            {
                _fieldName = fieldName;
                _valueWriter = valueWriter;
                _valueGetter = valueGetter;
            }

            public void Write(StringBuilder builder, T target)
            {
                var value = _valueGetter(target);
                JsonWriter.StringWriter.Append(builder, _fieldName);
                builder.Append(':');
                _valueWriter.Append(builder, value);
            }
        }
    }
}

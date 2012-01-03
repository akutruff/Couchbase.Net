using System;
using System.Collections.Generic;

namespace Json
{
    public interface IJsonGroupParser<T>
    {
        void Parse(string json, T value);
        void Parse(string json, ref int index, T value);
    }

    public interface IJsonParsingOperations<T> : IJsonGroupParser<T>
    {
        IJsonParsingOperations<T> OnGuid(string fieldName, Action<T, Guid> onParsed);
        IJsonParsingOperations<T> OnGroup<TSubGroup>(string fieldName, Action<T, TSubGroup> onParsed, Func<T, TSubGroup> subGroupSelector, IJsonGroupParser<TSubGroup> subGroupParser);
        IJsonParsingOperations<T> OnString(string fieldName, Action<T, string> onParsed);
        IJsonParsingOperations<T> OnInt(string fieldName, Action<T, int> onParsed);
        IJsonParsingOperations<T> OnUInt(string fieldName, Action<T, uint> onParsed);
        IJsonParsingOperations<T> OnLong(string fieldName, Action<T, long> onParsed);
        IJsonParsingOperations<T> OnULong(string fieldName, Action<T, ulong> onParsed);
        IJsonParsingOperations<T> OnDouble(string fieldName, Action<T, double> onParsed);
        IJsonParsingOperations<T> OnFloat(string fieldName, Action<T, float> onParsed);
        IJsonParsingOperations<T> OnBool(string fieldName, Action<T, bool> onParsed);
        IJsonParsingOperations<T> OnArray<TElement>(string fieldName, Action<T, List<TElement>> onParsed, IJsonValueParser<TElement> elementParser);
    }

    public class JsonGroupParser<T> : IJsonParsingOperations<T>
    {
        Dictionary<HashedSubstring, IJsonFieldParsingCallback> _callbacks = new Dictionary<HashedSubstring, IJsonFieldParsingCallback>(HashedSubstringComparer.Default);
                
        public void Parse(string json, T parsed)
        {
            int currentIndex = 0;
            Parse(json, ref currentIndex, parsed);
        }

        public void Parse(string json, ref int index, T target)
        {
            Scanner.MovePastNextIgnoringWhitespace(json, ref index, '{');

            while (index < json.Length)
            {
                Scanner.SkipWhitespace(json, ref index);

                var character = json[index];

                switch (character)
                {
                    case '\"':
                        {
                            Scanner.SkipWhitespace(json, ref index);

                            Substring fieldNameSubstring;
                            Scanner.GetNextString(json, ref index, out fieldNameSubstring);
                            
                            Scanner.MovePastNextIgnoringWhitespace(json, ref index, ':');
                            Scanner.SkipWhitespace(json, ref index);

                            IJsonFieldParsingCallback callback;
                            if (_callbacks.TryGetValue(fieldNameSubstring.GetHashedSubstring(), out callback))
                            {
                                callback.Parse(target, json, ref index);
                            }
                            else
                            {
                                Scanner.SkipNextValue(json, ref index);
                            }

                            Scanner.SkipWhitespace(json, ref index);
                            
                            if (json[index] == ',')
                            {
                                ++index;
                            }
                        }
                        break;
                    case '}':
                        ++index;
                        return;
                    default:
                        throw new Exception("Invalid character: " + character);
                }
            }
        }

        public IJsonParsingOperations<T> OnString(string fieldName, Action<T, string> onParsed)
        {
            AddCallback(fieldName, JsonParser.StringParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnInt(string fieldName, Action<T, int> onParsed)
        {
            AddCallback(fieldName, JsonParser.IntParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnBool(string fieldName, Action<T, bool> onParsed)
        {
            AddCallback(fieldName, JsonParser.BoolParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnArray<TElement>(string fieldName, Action<T, List<TElement>> onParsed, IJsonValueParser<TElement> elementParser)
        {
            AddCallback(fieldName, new JsonArrayParser<TElement>(elementParser), onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnUInt(string fieldName, Action<T, uint> onParsed)
        {
            AddCallback(fieldName, JsonParser.UIntParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnLong(string fieldName, Action<T, long> onParsed)
        {
            AddCallback(fieldName, JsonParser.LongParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnULong(string fieldName, Action<T, ulong> onParsed)
        {
            AddCallback(fieldName, JsonParser.ULongParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnDouble(string fieldName, Action<T, double> onParsed)
        {
            AddCallback(fieldName, JsonParser.DoubleParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnFloat(string fieldName, Action<T, float> onParsed)
        {
            AddCallback(fieldName, JsonParser.FloatParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnGuid(string fieldName, Action<T, Guid> onParsed)
        {
            AddCallback(fieldName, JsonParser.GuidParser, onParsed);
            return this;
        }

        public IJsonParsingOperations<T> OnGroup<TSubGroup>(string fieldName, Action<T, TSubGroup> onParsed, Func<T, TSubGroup> subGroupSelector, IJsonGroupParser<TSubGroup> subGroupParser)
        {
            _callbacks[new HashedSubstring(fieldName)] = new JsonSubGroupParsingCallback<TSubGroup>(subGroupParser, onParsed, subGroupSelector);
            return this;
        }

        public void AddCallback<TValue>(string fieldName, IJsonValueParser<TValue> jsonValueParser, Action<T, TValue> onParsed)
        {
            _callbacks[new HashedSubstring(fieldName)] = new JsonFieldParsingCallback<TValue>(jsonValueParser, onParsed);
        }

        private interface IJsonFieldParsingCallback
        {
            void Parse(T target, string json, ref int index);
        }

        private class JsonFieldParsingCallback<TValue> : IJsonFieldParsingCallback
        {
            private IJsonValueParser<TValue> _valueParser;
            public Action<T, TValue> _callback;

            public JsonFieldParsingCallback(IJsonValueParser<TValue> valueParser, Action<T, TValue> callback)
            {
                _valueParser = valueParser;
                _callback = callback;
            }

            public void Parse(T target, string json, ref int index)
            {
                var value = _valueParser.Parse(json, ref index);
                _callback(target, value);
            }
        }

        private class JsonSubGroupParsingCallback<TSubGroup> : IJsonFieldParsingCallback
        {
            private IJsonGroupParser<TSubGroup> _valueParser;
            public Action<T, TSubGroup> _callback;
            private readonly Func<T, TSubGroup> _subGroupSelector;

            public JsonSubGroupParsingCallback(IJsonGroupParser<TSubGroup> valueParser, Action<T, TSubGroup> callback, Func<T, TSubGroup> subGroupSelector)
            {
                _subGroupSelector = subGroupSelector;
                _valueParser = valueParser;
                _callback = callback;
            }

            public void Parse(T target, string json, ref int index)
            {
                var subGroup = _subGroupSelector(target);
                _valueParser.Parse(json, ref index, subGroup);
                _callback(target, subGroup);
            }
        }
    }
}

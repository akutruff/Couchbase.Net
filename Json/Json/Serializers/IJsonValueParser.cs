using System;
using System.Collections.Generic;
using System.Linq;

namespace Json
{
    public interface IJsonValueParser<T>
    {
        T Parse(string json, ref int index);
    }
}

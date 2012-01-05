using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public static class UriQueryBuilder
    {
        public static void AddToQuery(StringBuilder builder, string name, string value)
        {
            if (value != null)
            {
                builder.AppendFormat(builder.Length > 1 ? "&{0}={1}" : "{0}={1}", name, value);
            }
        }

        public static void AddJsonValueToQuery(StringBuilder builder, string name, string value)
        {
            if (value != null)
            {
                builder.AppendFormat(builder.Length > 1 ? "&{0}=\"{1}\"" : "{0}=\"{1}\"", name, value);
            }
        }

        public static void AddJsonValueToQuery(StringBuilder builder, string name, int value)
        {
            if (value != -1)
            {
                builder.AppendFormat(builder.Length > 1 ? "&{0}={1}" : "{0}={1}", name, value);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public static class UriBuilderExtensions
    {
        public static void AddToQuery(this UriBuilder builder, string name, string value)
        {
            if (value != null)
            {
                var formatString = builder.Query.Length > 1 ? "&{0}={1}" : "{0}={1}";
                builder.Query += string.Format(formatString, name, value);
            }
        }

        public static void AddJsonValueToQuery(this UriBuilder builder, string name, string value)
        {
            if (value != null)
            {
                var formatString = builder.Query.Length > 1 ? "&{0}=\"{1}\"" : "{0}=\"{1}\"";
                builder.Query += string.Format(formatString, name, value);
            }
        }

        public static void AddJsonValueToQuery(this UriBuilder builder, string name, int value)
        {
            if (value != -1)
            {
                var formatString = builder.Query.Length > 1 ? "&{0}={1}" : "{0}={1}";
                builder.Query += string.Format("{0}={1}", name, value); ;
            }
        }
    }
}

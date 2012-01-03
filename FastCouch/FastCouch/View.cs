using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public enum ViewStalenessOptions
    {
        NotStale,
        AllowStale,
        AllowStaleButUpdateAfter
    }

    public enum ViewSortDirection
    {
        Ascending,
        Descending
    }

    public class View
    {
        public string Document { get; private set; }
        public string Name { get; private set; }
        
        private readonly string _viewUriPath;

        private CouchbaseClient _client;

        internal View(CouchbaseClient client, string document, string name)
        {
            Document = document;
            Name = name;

            _client = client;

            _viewUriPath = "default/_design/" + document + "/_view/" + name;
        }

        public void Get(
            Action<ResponseStatus, string, object> callback,
            object state,
            string startKey = null,
            string endKey = null,
            int limit = -1,
            int skip = -1,
            int timeOut = -1,
            ViewSortDirection sortDirection = ViewSortDirection.Ascending,
            ViewStalenessOptions stalenessOptions = ViewStalenessOptions.NotStale)
        {
            var uriBuilder = GetUrlForQuery(startKey, endKey, limit, skip, timeOut, sortDirection, stalenessOptions);
            _client.ExecuteHttpQuery(uriBuilder, callback, state);
        }

        private UriBuilder GetUrlForQuery(string startKey, string endKey, int limit, int skip, int timeOut, ViewSortDirection sortDirection, ViewStalenessOptions stalenessOptions)
        {
            UriBuilder builder = new UriBuilder();
            builder.Path = _viewUriPath;

            builder.AddJsonValueToQuery("start_key", startKey);
            builder.AddJsonValueToQuery("end_key", endKey);
            builder.AddJsonValueToQuery("limit", limit);
            builder.AddJsonValueToQuery("skip", skip);
            builder.AddJsonValueToQuery("connection_timeout", timeOut);

            switch (sortDirection)
            {
                case ViewSortDirection.Ascending:
                    break;
                case ViewSortDirection.Descending:
                    builder.AddToQuery("descending", "true");
                    break;
            }

            switch (stalenessOptions)
            {
                case ViewStalenessOptions.NotStale:
                    break;
                case ViewStalenessOptions.AllowStale:
                    builder.AddToQuery("stale", "ok");
                    break;
                case ViewStalenessOptions.AllowStaleButUpdateAfter:
                    builder.AddToQuery("stale", "update_after");
                    break;
            }

            return builder;
        }
    }
}
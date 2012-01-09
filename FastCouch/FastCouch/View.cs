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
            string key = null,
            string startKey = null,
            string endKey = null,
            int limit = -1,
            int skip = -1,
            int timeOut = -1,
            ViewSortDirection sortDirection = ViewSortDirection.Ascending,
            ViewStalenessOptions stalenessOptions = ViewStalenessOptions.NotStale)
        {
            var uriBuilder = GetUrlForQuery(key, startKey, endKey, limit, skip, timeOut, sortDirection, stalenessOptions);
            _client.ExecuteHttpQuery(uriBuilder, !string.IsNullOrEmpty(key) ? key : !string.IsNullOrEmpty(startKey) ? startKey : endKey, callback, state);
        }

        //Optimize for multi key gets, so that they may batched into one query
        //public void EnqueueMultiKeyGet(
        //    Action<ResponseStatus, string, object> callback,
        //    object state,
        //    string key = null,
        //    string startKey = null,
        //    string endKey = null,
        //    int limit = -1,
        //    int skip = -1,
        //    int timeOut = -1,
        //    ViewSortDirection sortDirection = ViewSortDirection.Ascending,
        //    ViewStalenessOptions stalenessOptions = ViewStalenessOptions.NotStale)
        //{
        //    var uriBuilder = GetUrlForQuery(key, startKey, endKey, limit, skip, timeOut, sortDirection, stalenessOptions);
        //    _client.ExecuteHttpQuery(uriBuilder, !string.IsNullOrEmpty(key) ? key : !string.IsNullOrEmpty(startKey) ? startKey : endKey, callback, state);
        //}

        private UriBuilder GetUrlForQuery(string key, string startKey, string endKey, int limit, int skip, int timeOut, ViewSortDirection sortDirection, ViewStalenessOptions stalenessOptions)
        {
            StringBuilder stringBuilder = new StringBuilder();
            UriBuilder builder = new UriBuilder();
            builder.Path = _viewUriPath;

            UriQueryBuilder.AddJsonValueToQuery(stringBuilder, "key", key);
            UriQueryBuilder.AddJsonValueToQuery(stringBuilder, "start_key", startKey);
            UriQueryBuilder.AddJsonValueToQuery(stringBuilder, "end_key", endKey);
            UriQueryBuilder.AddJsonValueToQuery(stringBuilder, "limit", limit);
            UriQueryBuilder.AddJsonValueToQuery(stringBuilder, "skip", skip);
            UriQueryBuilder.AddJsonValueToQuery(stringBuilder, "connection_timeout", timeOut);

            switch (sortDirection)
            {
                case ViewSortDirection.Ascending:
                    break;
                case ViewSortDirection.Descending:
                    UriQueryBuilder.AddToQuery(stringBuilder, "descending", "true");
                    break;
            }

            switch (stalenessOptions)
            {
                case ViewStalenessOptions.NotStale:
                    break;
                case ViewStalenessOptions.AllowStale:
                    UriQueryBuilder.AddToQuery(stringBuilder, "stale", "ok");
                    break;
                case ViewStalenessOptions.AllowStaleButUpdateAfter:
                    UriQueryBuilder.AddToQuery(stringBuilder, "stale", "update_after");
                    break;
            }
            builder.Query = stringBuilder.ToString();
            return builder;
        }
    }
}
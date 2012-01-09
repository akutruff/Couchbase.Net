using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace FastCouch.Tests.Mocks
{
    public class StreamingClusterDataService
    {
        private HttpListener _listener;
        public int Port { get; set; }

        private AsyncPattern<HttpListener> _listenerAsync;

        public StreamingClusterDataService(int port)
        {
            Port = port;

            _listenerAsync = AsyncPattern.Create(
                (listener, pattern) => listener.BeginGetContext(pattern.OnCompleted, null),
                OnBeginGetContextReceived,
                OnError);
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://www.contoso.com:8080/");

            _listenerAsync.BeginAsync(_listener);
        }

        private HttpListener OnBeginGetContextReceived(IAsyncResult result)
        {
            _listener.EndGetContext(result);

            return _listener;
        }

        private HttpListener OnError(IAsyncResult result, Exception e)
        {
            Console.WriteLine("Error");
            return null;
        }
    }
}

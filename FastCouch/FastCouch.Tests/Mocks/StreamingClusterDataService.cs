using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;
using System.Text;
using System.IO;

namespace FastCouch.Tests.Mocks
{
    public class StreamingClusterDataService : IDisposable
    {
        private HttpListener _listener;

        public int StreamingPort { get; private set; }
        public string Host { get; private set; }

        public string LastKnownClusterData { get; set; }

        private object _gate = new object();

        private HttpListenerResponse _response;

        private AsyncPattern<HttpListener> _listenerAsync;

        public StreamingClusterDataService(string host, int port)
        {
            Host = host;
            StreamingPort = port;

            _listenerAsync = AsyncPattern.Create(
                (listener, pattern) => listener.BeginGetContext(pattern.OnCompleted, null),
                OnBeginGetContextReceived,
                OnBeginGetContextError);
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://" + this.Host + ":" + this.StreamingPort + "/");

            _listener.Start();

            _listenerAsync.BeginAsync(_listener);
        }

        private HttpListener OnBeginGetContextReceived(IAsyncResult result)
        {
            if (_response != null)
            {
                try
                {
                    _response.Close();
                    _response = null;
                }
                catch
                { }
            }

            var context = _listener.EndGetContext(result);
            var request = context.Request;

            var url = request.Url;
            //Console.WriteLine("streaming request: " + request.Url);

            lock (_gate)
            {
                _response = context.Response;
            }
            try
            {
                SendClusterData();
            }
            catch(Exception e)
            {
                Console.WriteLine("Error sending cluster data");
                Console.WriteLine(e);

                _response.Close();
                _response = null;
            }

            return _listener;
        }

        private void SendClusterData()
        {
            lock (_gate)
            {
                if (_response != null && !string.IsNullOrEmpty(this.LastKnownClusterData))
                {
                    var responseBuffer = Encoding.UTF8.GetBytes(this.LastKnownClusterData);
                    _response.OutputStream.Write(responseBuffer, 0, responseBuffer.Length);
                }
            }
        }

        public void UpdateCluster(string clusterData)
        {
            lock (_gate)
            {
                this.LastKnownClusterData = clusterData;
                SendClusterData();
            }
        }

        private HttpListener OnBeginGetContextError(IAsyncResult result, Exception e)
        {
            //Console.WriteLine("Error " + e);
            Dispose();
            return null;
        }

        public void Dispose()
        {
            try
            {
                _listener.Close();
            }
            catch
            {
            }
        }
    }
}

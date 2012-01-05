using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    //Treat this as an immutable object.
    public class Server : IDisposable
    {
        public string HostName { get; private set; }
        public int StreamingPort { get { return 8091; } }
        //public int ViewPort { get { return 5984; } }
        public int ViewPort { get { return 8092; } }

        public int Port { get; private set; }

        public MemcachedClient MemcachedClient { get; set; }
        public HttpClient ViewHttpClient { get;  set; }
        public HttpClient StreamingHttpClient { get; set; }

        public Server(string hostName, int port)
        {
            HostName = hostName;
            Port = port;
        }

        public Server(string hostName)
            : this(hostName, 11210)
        {
        }

        public bool TrySend(MemcachedCommand command)
        {
            return this.MemcachedClient.TrySend(command);
        }

        public bool TrySend(HttpCommand httpCommand)
        {
            return ViewHttpClient.TrySend(httpCommand);
        }

        public void Connect(
            Action<string, MemcachedCommand> onRecoverableError, 
            Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> onDisconnected, 
            Action<string, HttpCommand> onHttpFailure)
        {
            if (this.MemcachedClient == null)
            {
                MemcachedClient client = CreateNewMemcachedClient(onRecoverableError, onDisconnected);
                this.MemcachedClient = client;
            }
            
            if (this.ViewHttpClient == null)
            {
                this.ViewHttpClient = new HttpClient(this.HostName, this.ViewPort, onHttpFailure);
            }
        }
        
        public void ConnectStreamingClient(Action<string, HttpCommand> onStreamingFailure)
        {
            StreamingHttpClient = new HttpClient(this.HostName, this.StreamingPort, onStreamingFailure);
        }
        
        public MemcachedClient CreateNewMemcachedClient(Action<string, MemcachedCommand> onRecoverableError, Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> onDisconnected)
        {
            MemcachedClient client = new MemcachedClient(this.HostName, this.Port);
            client.OnRecoverableError += onRecoverableError;
            client.OnDisconnected += onDisconnected;
            client.Connect();
            return client;
        }

        public void Dispose()
        {
            if (this.MemcachedClient != null)
            {
                this.MemcachedClient.Dispose();
            }
            
            if (this.StreamingHttpClient != null)
            {
                this.StreamingHttpClient.Dispose();
            }

            if (this.ViewHttpClient != null)
            {
                this.ViewHttpClient.Dispose();
            }
        }

        public Server Clone()
        {
            return new Server(this.HostName, this.Port)
                {
                    MemcachedClient = this.MemcachedClient,
                    ViewHttpClient = this.ViewHttpClient,
                    StreamingHttpClient = this.StreamingHttpClient,
                };
        }
    }
}

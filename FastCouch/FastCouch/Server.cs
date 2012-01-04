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
        public int ViewPort { get { return 5984; } }
        
        public int Port { get; private set; }

        public MemcachedClient MemcachedClient { get; set; }
        public HttpClient HttpClient { get;  set; }

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
            return HttpClient.TrySend(httpCommand);
        }

        public void Connect(
            Action<string, MemcachedCommand> onRecoverableError, 
            Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> onDisconnected, 
            Action<string, HttpCommand> onHttpFailure)
        {
            MemcachedClient client = CreateNewMemcachedClient(onRecoverableError, onDisconnected);
            this.MemcachedClient = client;

            ConnectHttpClient(onHttpFailure);
        }
        
        public void ConnectHttpClient(Action<string, HttpCommand> onHttpFailure)
        {
            HttpClient = new HttpClient(this.HostName, this.ViewPort, onHttpFailure);
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
        }

        public Server Clone()
        {
            return new Server(this.HostName, this.Port)
                {
                    MemcachedClient = this.MemcachedClient,
                    HttpClient = this.HttpClient
                };
        }
    }
}

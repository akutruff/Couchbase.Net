using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
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
            : this(hostName, 1021)
        {
            //"http://127.0.0.1:5984/default/_design/dev_DocumentOne/_view/SimpleMap?full_set=true&connection_timeout=60000&limit=10&skip=0";
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
            this.MemcachedClient.Dispose();
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

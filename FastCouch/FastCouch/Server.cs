using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    //Treat this as an immutable object.
    public class Server : IDisposable
    {
        public string Id { get; private set; }
        
        public string HostName { get; private set; }

        public int StreamingPort { get; set; }
        public int ApiPort { get; set; }

        public int MemcachedPort { get; set; }

        public MemcachedClient MemcachedClient { get; set; }
        public HttpClient ViewHttpClient { get;  set; }
        public HttpClient StreamingHttpClient { get; set; }

        public Server(string hostName, int memcachedPort, int streamingPort, int apiPort)
        {
            HostName = hostName;
            this.MemcachedPort = memcachedPort;
            this.StreamingPort = streamingPort;
            this.ApiPort = apiPort;
            this.Id = GetId(hostName, memcachedPort);
        }

        public Server(string hostName, int memcachedPort)
            : this(hostName, memcachedPort, 8091, 8092)
        {
        }

        public Server(string hostName)
            : this(hostName, 11210, 8091, 8092)
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
                this.ViewHttpClient = new HttpClient(this.Id, this.HostName, this.ApiPort, onHttpFailure);
            }
        }
        
        public void ConnectStreamingClient(Action<string, HttpCommand> onStreamingFailure)
        {
            StreamingHttpClient = new HttpClient(this.Id, this.HostName, this.StreamingPort, onStreamingFailure);
        }
        
        public MemcachedClient CreateNewMemcachedClient(Action<string, MemcachedCommand> onRecoverableError, Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> onDisconnected)
        {
            MemcachedClient client = new MemcachedClient(this.Id, this.HostName, this.MemcachedPort);
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

        public static string GetId(string hostName, int memcachedPort)
        {
            return String.Intern(hostName + memcachedPort);
        }

        public Server Clone()
        {
            return new Server(this.HostName, this.MemcachedPort, this.StreamingPort, this.ApiPort)
                {
                    Id = this.Id,
                    MemcachedClient = this.MemcachedClient,
                    ViewHttpClient = this.ViewHttpClient,
                    StreamingHttpClient = this.StreamingHttpClient,
                };
        }
    }
}

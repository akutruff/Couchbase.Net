using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using Json;
namespace FastCouch
{
    public class CouchbaseClient : IDisposable
    {
        private const int MaxReconnectAttempts = 10;
        
        private bool _hasQuit = false;
        private bool _hasBeenDisposed;

        BufferPool<byte> _sendingBufferPool = new BufferPool<byte>(numberOfBuffers: 1000, bufferSize: 1024);

        private static readonly IJsonParsingOperations<Cluster> ClusterParser;

        private object _gate = new object();

        private volatile Cluster _cluster;

        private Thread _serverStreamingThread;
        private readonly string _bucketName;

        private int _currentCommandId = Int32.MinValue;

        private bool _hasClusterVBucketMapBeenUpdatedAtLeastOnce;

        private int _nextServerToUseForHttpQuery = Int32.MinValue;

        public CouchbaseClient(string bucketName, params Server[] servers)
        {
            _bucketName = bucketName;
            var cluster = new Cluster();

            foreach (var server in servers)
            {
                cluster.AddServer(server);
            }

            _cluster = cluster;

            _serverStreamingThread = new Thread(_ => StartStreamingServerMap());

            _serverStreamingThread.IsBackground = true;

            _serverStreamingThread.Start();
        }

        static CouchbaseClient()
        {
            ClusterParser = JsonParser.Create<Cluster>()
                .OnGroup<Cluster>("vBucketServerMap", (cluster, value) => { }, cluster => cluster, JsonParser.Create<Cluster>()
                    .OnArray("serverList", (cluster, serverNameAndPorts) =>
                    {
                        for (int i = 0; i < serverNameAndPorts.Count; i++)
                        {
                            var hostNameAndPortStrings = serverNameAndPorts[i].Split(':');

                            string hostName = hostNameAndPortStrings[0];
                            int port = Int32.Parse(hostNameAndPortStrings[1]);

                            var server = new Server(hostName, port);

                            cluster.AddServer(server);
                        }
                    }, JsonParser.StringParser)
                    .OnArray("vBucketMap", (cluster, value) => cluster.SetVBucketToServerMap(value), new JsonArrayParser<int>(JsonParser.IntParser))
                    .OnArray("vBucketMapForward", (cluster, value) => cluster.SetFastForwardVBucketToServerMap(value), new JsonArrayParser<int>(JsonParser.IntParser)));
        }

        public void StartStreamingServerMap()
        {
            Random rand = new Random();

            bool shouldKeepStreaming = true;
            while (shouldKeepStreaming)
            {
                var currentCluster = _cluster;

                var server = currentCluster.Servers[rand.Next(currentCluster.Servers.Count)];

                try
                {
                    var requestUrl = String.Format("http://{0}:{1}/pools/default/bucketsStreaming/{2}", server.HostName, server.StreamingPort, _bucketName);
                    var request = HttpWebRequest.Create(requestUrl);

                    //TODO: are credentials required?

                    var response = request.GetResponse();
                    var stream = response.GetResponseStream();

                    using (var reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            var serverMapJson = reader.ReadLine();
                            if (serverMapJson.Length > 0)
                            {
                                Cluster newCluster;

                                lock (_gate)
                                {
                                    if (_hasQuit)
                                    {
                                        shouldKeepStreaming = false;
                                        break;
                                    }
                                    //Console.WriteLine(serverMapJson);
                                    newCluster = new Cluster();

                                    ClusterParser.Parse(serverMapJson, newCluster);
                                    newCluster.ConnectMemcachedClients(currentCluster, OnErrorReceived, OnDisconnected, OnHttpFailure);

                                    _cluster = newCluster;

                                    _hasClusterVBucketMapBeenUpdatedAtLeastOnce = true;
                                    Monitor.Pulse(_gate);
                                }

                                if (newCluster.Servers.FirstOrDefault(x => x.HostName == server.HostName) == null)
                                {
                                    //server is no longer in cluster, so begin trying to stream from rest of cluster group
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        public void WaitForInitialClusterUpdate()
        {
            lock (_gate)
            {
                var cluster = _cluster;
                while (!_hasClusterVBucketMapBeenUpdatedAtLeastOnce)
                {
                    Monitor.Wait(_gate);
                    cluster = _cluster;
                }
            }
        }

        public void Get(string key, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new GetCommand(Interlocked.Increment(ref _currentCommandId), key, state, onComplete);

            SendCommand(command);
        }

        public void Set(string key, string value, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new SetCommand(Interlocked.Increment(ref _currentCommandId), key, value, 0, state, onComplete);

            SendCommand(command);
        }

        public void CheckAndSet(string key, string value, long cas, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new SetCommand(Interlocked.Increment(ref _currentCommandId), key, value, cas, state, onComplete);

            SendCommand(command);
        }

        public void Delete(string key, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new DeleteCommand(Interlocked.Increment(ref _currentCommandId), key, state, onComplete);

            SendCommand(command);
        }

        public void Quit()
        {
            Cluster cluster;

            lock (_gate)
            {
                _hasQuit = true;

                cluster = _cluster;
            }

            foreach (var server in cluster.Servers)
            {
                var command = new QuitCommand(Interlocked.Increment(ref _currentCommandId));

                server.TrySend(command);
            }
        }

        private void SendCommand(MemcachedCommand command)
        {
            Cluster cluster = _cluster;

            command.SetVBucketId(cluster.GetVBucket(command.Key));

            var serversForVbucket = cluster.GetServersForVBucket(command.VBucketId);

            SendCommand(command, serversForVbucket);
        }
        
        private static void SendCommand(MemcachedCommand command, List<Server> serversForVbucket)
        {
            for (int iServer = 0; iServer < serversForVbucket.Count; iServer++)
            {
                if (serversForVbucket[iServer].TrySend(command))
                {
                    return;
                }
            }

            throw new Exception("All known servers and replicas for vBucket are disconnected.");
        }

        private void OnDisconnected(string hostName, IEnumerable<MemcachedCommand> pendingSends, IEnumerable<MemcachedCommand> pendingReceives)
        {
            //TODO: This may be an edge case, but it seems completely possible that a node failure would leave some pendingOperations in an indeterminate state
            //  For now I'm just going to tell the client that some serious shit just happened and it's up to them if they want
            //  to check and make sure that all is cool in the database.

            foreach (var command in pendingReceives)
            {
                command.ResponseStatus = ResponseStatus.DisconnectionOccuredWhileOperationPending;
                command.NotifyComplete();
            }

            for (int i = 0; i < MaxReconnectAttempts; i++)
            {
                Cluster cluster;

                lock (_gate)
                {
                    if (_hasQuit)
                        return;

                    cluster = _cluster;
                }

                var serverWithDisconnectedMemcachedClient = cluster.GetServerByHostname(hostName);

                if (serverWithDisconnectedMemcachedClient == null || 
                    TryToReconnectServerAndUpdateCluster(serverWithDisconnectedMemcachedClient))
                {
                    break;
                }

                Thread.Sleep(100);
            }
            
            //Pending sends are okay to resend because there is no chance that a server has even began to handle the request.
            foreach (var command in pendingSends)
            {
                SendCommand(command);
            }
        }

        private bool TryToReconnectServerAndUpdateCluster(Server serverWithDisconnectedMemcachedClient)
        {
            MemcachedClient newMemcachedClient;

            try
            {
                newMemcachedClient = serverWithDisconnectedMemcachedClient.CreateNewMemcachedClient(OnErrorReceived, OnDisconnected);
            }
            catch
            {
                //Exception means a failed connection attempt by the network, so we wait a bit and retry things again.
                return false;
            }

            lock (_gate)
            {
                var cluster = _cluster;
                if (_hasQuit)
                {
                    newMemcachedClient.Dispose();
                    return false;
                }

                //Update the cluster after we have successfully reconnected.
                var newCluster = cluster.Clone();

                var serverInNewCluster = newCluster.GetServerByHostname(serverWithDisconnectedMemcachedClient.HostName);

                //server no longer in cluster after update
                if (serverInNewCluster == null)
                {
                    newMemcachedClient.Dispose();
                    return false;
                }

                serverInNewCluster.MemcachedClient = newMemcachedClient;

                _cluster = newCluster;
                return true;
            }
        }

        private void OnErrorReceived(string hostName, MemcachedCommand command)
        {
            switch (command.ResponseStatus)
            {
                case ResponseStatus.VbucketBelongsToAnotherServer:
                    OnWrongVBucketForServer(hostName, command);
                    break;
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    throw new NotImplementedException("Need to implement retry on same server");
                //break;
                case ResponseStatus.NoError:
                    throw new Exception("Should be impossible to get here...");
                default:
                    //The error is not something we can deal with inside the library itself, the caller screwed up.
                    command.NotifyComplete();
                    break;
            }
        }

        private void OnWrongVBucketForServer(string hostNameOfLastServerTriedToSendTo, MemcachedCommand command)
        {
            Cluster cluster = _cluster;

            var fastForwardedServers = cluster.GetFastForwardedServersForVBucket(command.VBucketId);
            if (fastForwardedServers != null)
            {
                SendCommand(command, fastForwardedServers);
            }
            else
            {
                SendByLinearlyPollingServers(cluster, hostNameOfLastServerTriedToSendTo, command);
            }
        }
        
        private static void SendByLinearlyPollingServers(Cluster cluster, string hostNameOfLastServerTriedToSendTo, MemcachedCommand command)
        {
            int indexOfServerToTry = GetIndexOfNextServerInCluster(cluster, hostNameOfLastServerTriedToSendTo);
            for (int i = 0; i < cluster.Servers.Count - 1; i++)
            {
                if (cluster.Servers[indexOfServerToTry].TrySend(command))
                {
                    return;
                }
                indexOfServerToTry = (indexOfServerToTry + 1) % cluster.Servers.Count;
            }

            throw new Exception("No servers connected");
        }
        
        private static int GetIndexOfNextServerInCluster(Cluster cluster, string hostNameOfLastServerTriedToSendTo)
        {
            var indexOfLastServerTriedToSendTo = cluster.Servers.FindIndex(x => x.HostName == hostNameOfLastServerTriedToSendTo);

            int indexOfServerToTry = indexOfLastServerTriedToSendTo == -1 ? 0 : (indexOfLastServerTriedToSendTo + 1) % cluster.Servers.Count;
            return indexOfServerToTry;
        }


        public View GetView(string document, string name)
        {
            return new View(this, document, name);
        }

        public void ExecuteHttpQuery(UriBuilder uriBuilder, Action<ResponseStatus, string, object> callback, object state)
        {
            Cluster cluster = _cluster;
            
            HttpCommand httpCommand = new HttpCommand(uriBuilder, state, callback);

            TrySendToFirstAvailableServer(cluster, httpCommand);
        }
        
        private void TrySendToFirstAvailableServer(Cluster cluster, HttpCommand httpCommand)
        {
            for (int i = 0; i < cluster.Servers.Count; i++)
            {
                var serverToUseForHttpQuery = GetNextServerToUseForHttpQuery(cluster);
                if (serverToUseForHttpQuery.TrySend(httpCommand))
                {
                    return;
                }
            }
            
            throw new Exception("No servers available");
        }

        private Server GetNextServerToUseForHttpQuery(Cluster cluster)
        {
            uint indexOfNextServer = ((uint)Interlocked.Increment(ref _nextServerToUseForHttpQuery)) % (uint)cluster.Servers.Count;
            var server = cluster.Servers[(int)indexOfNextServer];
            return server;
        }

        private void OnHttpFailure(string hostName, HttpCommand command)
        {
            var cluster = _cluster;
            lock (_gate)
            {
                if (_hasQuit)
                {
                    return;
                }
            }
            TrySendToFirstAvailableServer(cluster, command);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                var cluster = _cluster;

                if (_hasBeenDisposed)
                {
                    return;
                }

                _hasBeenDisposed = true;
                _hasQuit = true;
                
                foreach (var server in cluster.Servers)
                {
                    server.Dispose();
                }
            }

            _serverStreamingThread.Abort();
        }
    }
}
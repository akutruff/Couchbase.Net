using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using Json;
using System.Diagnostics;
using System.Net.Sockets;

namespace FastCouch
{
    public class CouchbaseClient : IDisposable
    {
        private bool _hasQuit;
        private bool _hasBeenDisposed;

        private object _gate = new object();

        private volatile Cluster _cluster;

        private readonly string _bucketName;

        private int _currentCommandId = Int32.MinValue;

        private bool _hasClusterVBucketMapBeenUpdatedAtLeastOnce;

        private int _nextServerToUseForHttpQuery = Int32.MinValue;

        private Dictionary<string, Cluster> _activeServersToLastReportedClusterDefinition = new Dictionary<string, Cluster>();

        private string _configurationAuthorityServerId = string.Empty;

        private AsyncPattern<TcpClient, ReconnectAttempt> _reconnectMemcachedAsync;

        public CouchbaseClient(string bucketName, params Server[] servers)
        {
            lock (_gate)
            {
                _reconnectMemcachedAsync = AsyncPattern.Create<TcpClient, ReconnectAttempt>(
                    BeginMemcachedReconnection,
                    OnMemcacachedReconnectionCompleted,
                    OnMemcachedReconnectionError);

                _bucketName = bucketName;
                var cluster = new Cluster();

                foreach (var server in servers)
                {
                    cluster.AddServer(server);
                }
                
                BeginStreamingFromNewServersInCluster(cluster);
                
                _cluster = cluster;
            }
        }

        //Should be in lock when doing this.
        private void BeginStreamingFromNewServersInCluster(Cluster cluster)
        {
            foreach (var server in cluster.Servers)
            {
                if (server.StreamingHttpClient == null)
                {
                    server.ConnectStreamingClient(OnServerStreamingMapDisconnected);
                    RequestServerStreamingMap(server);
                }
            }
        }

        //Should be in lock when doing this
        private void RequestServerStreamingMap(Server server)
        {
            string serverId = server.Id;
            var uri = new UriBuilder { Path = String.Format("pools/default/bucketsStreaming/{2}", server.HostName, server.StreamingPort, _bucketName) };

            var streamingCommand = new LineReadingHttpCommand(uri, null, (json, _) => OnServerStreamMapUpdated(serverId, json), (response, value, state) => { });
            server.StreamingHttpClient.TrySend(streamingCommand);
        }

        public bool OnServerStreamMapUpdated(string serverId, string json)
        {
            //Console.WriteLine("J: " + json);

            if (string.IsNullOrEmpty(json))
                return true;

            Cluster parsedClusterDefinition;

            //Console.WriteLine(json);
            //Console.WriteLine();

            try
            {
                parsedClusterDefinition = new Cluster();
                ClusterParser.Instance.Parse(json, parsedClusterDefinition);
            }
            catch
            {
                //Console.WriteLine(json);
                throw;
            }

            lock (_gate)
            {
                if (_hasQuit)
                {
                    return false;
                }

                Cluster currentCluster = _cluster;

                if (currentCluster.GetServerById(serverId) == null)
                {
                    return false;
                }

                _activeServersToLastReportedClusterDefinition[serverId] = parsedClusterDefinition;

                if (string.IsNullOrEmpty(_configurationAuthorityServerId))
                {
                	_configurationAuthorityServerId = serverId;
                }

                UpdateConfigurationAuthorityServerBasedOnMostAvailableServer();

                Console.WriteLine("report from: " + serverId + " currentAuthority: " + _configurationAuthorityServerId);
                if (serverId == _configurationAuthorityServerId)
                {
                    var newAuthorityClusterDefinition = _activeServersToLastReportedClusterDefinition[_configurationAuthorityServerId];

                    //Clone so that the table of _activeServersToLastReportedClusterConfig does not have references to tcpClient, htttpClient, etc.
                    var newAuthorityCluster = newAuthorityClusterDefinition.Clone();

                    newAuthorityCluster.ConfigureClusterFromExistingClusterAndCommitAnyConnectionChanges(
                        currentCluster,
                        OnPossiblyRecoverableMemcachedError,
                        OnMemcachedDisconnection,
                        OnViewRequestError);

                    foreach (var activeServerId in _activeServersToLastReportedClusterDefinition.Keys.ToList())
                    {
                        if (newAuthorityClusterDefinition.GetServerById(activeServerId) == null)
                        {
                            _activeServersToLastReportedClusterDefinition.Remove(activeServerId);
                        }
                    }

                    foreach (var reconnectionAttempt in _currentlyUnderwayMemcachedReconnectAttempts.ToList())
                    {
                        if (newAuthorityClusterDefinition.GetServerById(reconnectionAttempt.Server.Id) == null)
                        {
                            _currentlyUnderwayMemcachedReconnectAttempts.Remove(reconnectionAttempt);
                            reconnectionAttempt.Cancel();
                        }
                    }

                    BeginStreamingFromNewServersInCluster(newAuthorityCluster);

                    _hasClusterVBucketMapBeenUpdatedAtLeastOnce = true;

                    currentCluster = newAuthorityCluster;
                    _cluster = currentCluster;

                    Monitor.Pulse(_gate);
                }

                bool isThisServerStillInTheCluster = _activeServersToLastReportedClusterDefinition.ContainsKey(serverId);

                return isThisServerStillInTheCluster;
            }
        }

        //Must be called within a lock
        private void UpdateConfigurationAuthorityServerBasedOnMostAvailableServer()
        {
            int mostMentionedCount = 0;
            string mostMentionedServerId = string.Empty;

            var serverMentionCounts = new Dictionary<string, int>();
            foreach (var clusterDefinition in _activeServersToLastReportedClusterDefinition.Values)
            {
                for (int i = 0; i < clusterDefinition.Servers.Count; i++)
                {
                    var serverIdMentionedByCluster = clusterDefinition.Servers[i].Id;

                    int currentCount;
                    serverMentionCounts.TryGetValue(serverIdMentionedByCluster, out currentCount);

                    int newCount = currentCount + 1;
                    if (newCount > mostMentionedCount)
                    {
                        mostMentionedCount = newCount;
                        mostMentionedServerId = serverIdMentionedByCluster;
                    }

                    serverMentionCounts[serverIdMentionedByCluster] = newCount;
                }
            }

            int mentionCountOfCurrentAuthority;
            if (!serverMentionCounts.TryGetValue(_configurationAuthorityServerId, out mentionCountOfCurrentAuthority) ||
                mostMentionedCount > mentionCountOfCurrentAuthority)
            {
                _configurationAuthorityServerId = mostMentionedServerId;
            }
        }

        private void OnServerStreamingMapDisconnected(string serverId, HttpCommand command)
        {
            lock (_gate)
            {
                _activeServersToLastReportedClusterDefinition.Remove(serverId);
            }

            //Wait a bit and ensure that the retry is executed asynchronously.

            //TODO: AK maybe use a Timer instead of a wait? Probably better for the threadpool.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(1000);

                lock (_gate)
                {
                    Cluster cluster = _cluster;
                    var server = cluster.GetServerById(serverId);

                    if (!_hasQuit && server != null)
                    {
                        RequestServerStreamingMap(server);
                    }
                }
            });

        }

        public bool WaitForInitialClusterUpdate(int millisecondsTimeout)
        {
            //Logic here is complicated-ish because of spurious wakeups.  I don't trust that Monitor.Wait won't wakeup before it's timeout even though there hasn't been a pulse.
            lock (_gate)
            {
                var millisecondsToWait = millisecondsTimeout;
                var watch = Stopwatch.StartNew();

                while (!_hasClusterVBucketMapBeenUpdatedAtLeastOnce)
                {
                    if (!Monitor.Wait(_gate, millisecondsToWait))
                    {
                        return false;
                    }

                    int elapsed = (int)watch.ElapsedMilliseconds;
                    if (elapsed >= millisecondsTimeout)
                        break;

                    millisecondsToWait = millisecondsTimeout - elapsed;
                }
                return _hasClusterVBucketMapBeenUpdatedAtLeastOnce;
            }
        }

        public static CouchbaseClient Connect(string bucketName, int millisecondsToWaitForVBucketMap, string hostName, params string[] hostNames)
        {
            var servers = new List<Server> { new Server(hostName) };
            servers.AddRange(hostNames.Select(host => new Server(hostName)));

            return Connect(bucketName, millisecondsToWaitForVBucketMap, servers.ToArray());
        }
        
        public static CouchbaseClient Connect(string bucketName, int millisecondsToWaitForVBucketMap, params Server[] servers)
        {
            CouchbaseClient client;
            try
            {
                client = new CouchbaseClient(bucketName, servers);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to connect client", e);
            }

            if (!client.WaitForInitialClusterUpdate(millisecondsToWaitForVBucketMap))
            {
                throw new TimeoutException("Timeout expired while waiting for vbucket map.");
            }

            return client;
        }

        public void Get(string key, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new GetCommand(Interlocked.Increment(ref _currentCommandId), key, state, onComplete);

            SendCommandWithVBucketId(command);
        }

        public void Set(string key, string value, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new SetCommand(Interlocked.Increment(ref _currentCommandId), key, value, 0, state, onComplete);

            SendCommandWithVBucketId(command);
        }

        public void CheckAndSet(string key, string value, long cas, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new SetCommand(Interlocked.Increment(ref _currentCommandId), key, value, cas, state, onComplete);

            SendCommandWithVBucketId(command);
        }

        public void Delete(string key, Action<ResponseStatus, string, long, object> onComplete, object state)
        {
            var command = new DeleteCommand(Interlocked.Increment(ref _currentCommandId), key, state, onComplete);

            SendCommandWithVBucketId(command);
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
                var command = new QuitCommand(
                    Interlocked.Increment(ref _currentCommandId),
                    (response, value, cas, state) =>
                    {
                        server.Dispose();
                    });

                server.TrySend(command);
            }
        }

        private void SendCommandWithVBucketId(MemcachedCommand command)
        {
            Cluster cluster = _cluster;

            command.SetVBucketId(cluster.GetVBucket(command.Key));

            SendCommand(cluster, command);
        }

        private static void SendCommand(Cluster cluster, MemcachedCommand command)
        {
            if (!TrySendCommand(cluster, command))
            {
                command.NotifyComplete(ResponseStatus.DisconnectionOccuredWhileOperationWaitingToBeSent);
            }
        }

        private static bool TrySendCommand(Cluster cluster, MemcachedCommand command)
        {
            var serversForVbucket = cluster.GetServersForVBucket(command.VBucketId);

            return TrySendCommand(serversForVbucket, command);
        }

        private static bool TrySendCommand(List<Server> serversForVbucket, MemcachedCommand command)
        {
            for (int iServer = 0; iServer < serversForVbucket.Count; iServer++)
            {
                if (serversForVbucket[iServer].TrySend(command))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnMemcachedDisconnection(string serverId, IEnumerable<MemcachedCommand> pendingSends, IEnumerable<MemcachedCommand> pendingReceives)
        {
            NotifyCommands(pendingReceives, ResponseStatus.DisconnectionOccuredBeforeResponseReceived);

            if (GetHasQuit())
            {
                NotifyCommands(pendingSends, ResponseStatus.DisconnectionOccuredWhileOperationWaitingToBeSent);
                return;
            }

            Cluster cluster = _cluster;

            //Pending sends are okay to resend because there is no chance that a server has even began to handle the request.
            //  technically this could kick off a lot of "not my vbucket" errors if the cluster map doesn't come through right quick.
            foreach (var command in pendingSends)
            {
                //Send the command which will hopefully hit a connected replica.
                if (!TrySendCommand(cluster, command))
                {
                    command.NotifyComplete(ResponseStatus.DisconnectionOccuredWhileOperationWaitingToBeSent);
                }
            }

            TryToReconnectServerAndUpdateClusterIfServerStillExists(serverId);


        }

        private IAsyncResult BeginMemcachedReconnection(TcpClient tcpClient, AsyncPattern<TcpClient, ReconnectAttempt> pattern, ReconnectAttempt reconnectAttempt)
        {
            var result = tcpClient.BeginConnect(
                reconnectAttempt.Server.HostName,
                reconnectAttempt.Server.MemcachedPort,
                pattern.OnCompleted,
                reconnectAttempt);

            return result;
        }

        private HashSet<ReconnectAttempt> _currentlyUnderwayMemcachedReconnectAttempts = new HashSet<ReconnectAttempt>();

        private AsyncPatternResult<TcpClient, ReconnectAttempt> OnMemcacachedReconnectionCompleted(IAsyncResult result, ReconnectAttempt reconnectAttempt)
        {
            TcpClient tcpClient = reconnectAttempt.TcpClient;
            tcpClient.EndConnect(result);

            lock (_gate)
            {
                var cluster = _cluster;

                //Update the cluster after we have successfully reconnected.
                var newCluster = cluster.Clone();

                var serverInLatestCluster = newCluster.GetServerById(reconnectAttempt.Server.Id);

                if (_hasQuit ||
                    serverInLatestCluster == null ||
                    reconnectAttempt.HasBeenCanceled)
                {
                    _currentlyUnderwayMemcachedReconnectAttempts.Remove(reconnectAttempt);
                    reconnectAttempt.Cancel();
                }
                else
                {
                    MemcachedClient newMemcachedClient = new MemcachedClient(serverInLatestCluster.Id, serverInLatestCluster.HostName, serverInLatestCluster.MemcachedPort);

                    newMemcachedClient.OnDisconnected += OnMemcachedDisconnection;
                    newMemcachedClient.OnRecoverableError += OnPossiblyRecoverableMemcachedError;
                    newMemcachedClient.Connect(tcpClient);

                    serverInLatestCluster.MemcachedClient = newMemcachedClient;

                    _currentlyUnderwayMemcachedReconnectAttempts.Remove(reconnectAttempt);
                    _cluster = newCluster;
                }

                return _reconnectMemcachedAsync.Stop();
            }
        }

        private AsyncPatternResult<TcpClient, ReconnectAttempt> OnMemcachedReconnectionError(IAsyncResult result, ReconnectAttempt reconnectAttempt, Exception e)
        {
            lock (_gate)
            {
                var cluster = _cluster;

                var serverInLatestCluster = cluster.GetServerById(reconnectAttempt.Server.Id);

                if (_hasQuit ||
                    serverInLatestCluster == null ||
                    reconnectAttempt.HasBeenCanceled)
                {
                    _currentlyUnderwayMemcachedReconnectAttempts.Remove(reconnectAttempt);
                    reconnectAttempt.Cancel();
                }
                else
                {
                    const int millisecondsToWaitBetweenReconnectAttempts = 1000;

                    reconnectAttempt.Timer = new Timer(
                        OnReconnectTimerElapsed,
                        reconnectAttempt,
                        millisecondsToWaitBetweenReconnectAttempts,
                        Timeout.Infinite);
                }

                return _reconnectMemcachedAsync.Stop();
            }
        }

        public void OnReconnectTimerElapsed(object state)
        {
            ReconnectAttempt reconnectAttempt = (ReconnectAttempt)state; 
            
            lock (_gate)
            {
                if (!reconnectAttempt.HasBeenCanceled)
                {
                    _reconnectMemcachedAsync.BeginAsync(reconnectAttempt.TcpClient, reconnectAttempt);
                }
            }
        }

        private void TryToReconnectServerAndUpdateClusterIfServerStillExists(string serverId)
        {
            lock (_gate)
            {
                Console.WriteLine("reconnecting");
                Cluster cluster = _cluster;
                var serverWithDisconnectedMemcachedClient = cluster.GetServerById(serverId);

                if (_hasQuit ||
                    serverWithDisconnectedMemcachedClient == null)
                {
                    return;
                }

                TcpClient client = new TcpClient();
                var reconnectAttempt = new ReconnectAttempt(client, serverWithDisconnectedMemcachedClient);

                _reconnectMemcachedAsync.BeginAsync(client, reconnectAttempt);
            }
        }

        private void OnPossiblyRecoverableMemcachedError(string serverid, MemcachedCommand command)
        {
            var cluster = _cluster;

            switch (command.ResponseStatus)
            {
                case ResponseStatus.VbucketBelongsToAnotherServer:
                    OnWrongVBucketForServer(serverid, command);
                    break;
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    SendCommand(cluster, command); //Retry
                    break;
                default:
                    throw new Exception("Should be impossible to get here...");
            }
        }

        private void OnWrongVBucketForServer(string lastServerTriedToSendToId, MemcachedCommand command)
        {
            Cluster cluster = _cluster;

            var fastForwardedServers = cluster.GetFastForwardedServersForVBucket(command.VBucketId);

            if (fastForwardedServers == null ||
                !TrySendCommand(fastForwardedServers, command))
            {
                SendByLinearlyPollingServers(cluster, lastServerTriedToSendToId, command);
                return;
            }
        }

        private static void SendByLinearlyPollingServers(Cluster cluster, string lastServerTriedToSendToId, MemcachedCommand command)
        {
            int indexOfServerToTry = GetIndexOfNextServerInCluster(cluster, lastServerTriedToSendToId);
            for (int i = 0; i < cluster.Servers.Count; i++)
            {
                if (cluster.Servers[indexOfServerToTry].TrySend(command))
                {
                    return;
                }

                indexOfServerToTry = MathUtils.CircularIncrement(indexOfServerToTry, cluster.Servers.Count);
            }

            command.NotifyComplete(ResponseStatus.DisconnectionOccuredWhileOperationWaitingToBeSent);
        }

        private static int GetIndexOfNextServerInCluster(Cluster cluster, string lastServerTriedToSendToId)
        {
            var indexOfLastServerTriedToSendTo = cluster.Servers.FindIndex(x => x.Id == lastServerTriedToSendToId);

            int indexOfServerToTry = indexOfLastServerTriedToSendTo == -1 ? 0 : MathUtils.CircularIncrement(indexOfLastServerTriedToSendTo, cluster.Servers.Count);
            return indexOfServerToTry;
        }

        public View GetView(string document, string name)
        {
            return new View(this, document, name);
        }

        public void ExecuteViewHttpQuery(UriBuilder uriBuilder, string keyHintForSelectingServer, Action<ResponseStatus, string, object> callback, object state)
        {
            Cluster cluster = _cluster;

            HttpCommand httpCommand = new HttpCommand(uriBuilder, state, callback);

            TrySendingViewRequestToFirstAvailableServer(cluster, httpCommand, keyHintForSelectingServer);
        }

        private void TrySendingViewRequestToFirstAvailableServer(Cluster cluster, HttpCommand httpCommand, string keyHintForSelectingServer)
        {
            //We are going to use the key hint to help destribute which server should be getting the requests.
            if (!string.IsNullOrEmpty(keyHintForSelectingServer))
            {
                int vBucketId;
                var server = cluster.GetServer(keyHintForSelectingServer, out vBucketId);
                if (server.TrySend(httpCommand))
                {
                    return;
                }
            }

            TrySendViewRequestToFirstAvailableServer(cluster, httpCommand);
        }

        private void TrySendViewRequestToFirstAvailableServer(Cluster cluster, HttpCommand httpCommand)
        {
            for (int i = 0; i < cluster.Servers.Count; i++)
            {
                var serverToUseForQuery = GetNextServerToUseForQuery(cluster);
                if (serverToUseForQuery.TrySend(httpCommand))
                {
                    return;
                }
            }

            httpCommand.NotifyComplete(ResponseStatus.DisconnectionOccuredBeforeResponseReceived);
        }

        private Server GetNextServerToUseForQuery(Cluster cluster)
        {
            uint indexOfNextServer = ((uint)Interlocked.Increment(ref _nextServerToUseForHttpQuery)) % (uint)cluster.Servers.Count;
            var server = cluster.Servers[(int)indexOfNextServer];
            return server;
        }

        private void OnViewRequestError(string hostName, HttpCommand command)
        {
            var cluster = _cluster;
            if (GetHasQuit())
            {
                command.NotifyComplete(ResponseStatus.DisconnectionOccuredBeforeResponseReceived);
                return;
            }

            TrySendViewRequestToFirstAvailableServer(cluster, command);
        }

        private static void NotifyCommands(IEnumerable<MemcachedCommand> commands, ResponseStatus status)
        {
            foreach (var command in commands)
            {
                command.NotifyComplete(status);
            }
        }

        private bool GetHasQuit()
        {
            lock (_gate)
            {
                return _hasQuit;
            }
        }

        public void Dispose()
        {
            Cluster cluster;
            lock (_gate)
            {
                cluster = _cluster;

                if (_hasBeenDisposed)
                {
                    return;
                }

                _currentlyUnderwayMemcachedReconnectAttempts.Clear();
                _hasBeenDisposed = true;
                _hasQuit = true;
            }

            foreach (var server in cluster.Servers)
            {
                server.Dispose();
            }
        }
    }
}
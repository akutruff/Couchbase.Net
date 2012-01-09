﻿using System;
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

        public CouchbaseClient(string bucketName, params Server[] servers)
        {
            lock (_gate)
            {
                _bucketName = bucketName;
                var cluster = new Cluster();

                foreach (var server in servers)
                {
                    cluster.AddServer(server);
                }

                _cluster = cluster;

                BeginStreamingFromNewServersInCluster();
            }
        }

        public void BeginStreamingFromNewServersInCluster()
        {
            Cluster cluster = _cluster;

            foreach (var server in cluster.Servers)
            {
                if (server.StreamingHttpClient == null)
                {
                    server.ConnectStreamingClient(OnServerStreamingMapDisconnected);
                    RequestServerStreamingMap(server);
                }
            }
        }

        private void RequestServerStreamingMap(Server server)
        {
            string hostName = server.HostName;
            var uri = new UriBuilder { Path = String.Format("pools/default/bucketsStreaming/{2}", hostName, server.StreamingPort, _bucketName) };

            var streamingCommand = new LineReadingHttpCommand(uri, null, (json, _) => OnServerStreamMapUpdated(hostName, json), (response, value, state) => { });
            server.StreamingHttpClient.TrySend(streamingCommand);
        }

        public bool OnServerStreamMapUpdated(string hostName, string json)
        {
            //Console.WriteLine("J: " + json);

            if (string.IsNullOrEmpty(json))
                return true;

            var newCluster = new Cluster();
            ClusterParser.Instance.Parse(json, newCluster);

            lock (_gate)
            {
                if (_hasQuit)
                {
                    return false;
                }
                Cluster currentCluster = _cluster;

                newCluster.ConnectMemcachedClients(currentCluster, OnPossiblyRecoverableMemcachedError, OnDisconnection, OnViewRequestError);

                BeginStreamingFromNewServersInCluster();

                _cluster = newCluster;

                _hasClusterVBucketMapBeenUpdatedAtLeastOnce = true;
                Monitor.Pulse(_gate);
            }

            var isThisServerStillInTheCluster = newCluster.GetServerByHostName(hostName) != null;

            return isThisServerStillInTheCluster;
        }

        private void OnServerStreamingMapDisconnected(string hostName, HttpCommand command)
        {
            Cluster cluster = _cluster;
            if (!GetHasQuit() &&
                cluster.GetServerByHostName(hostName) != null)
            {
                //Wait a bit and ensure that the retry is executed asynchronously.
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    Thread.Sleep(1000);
                    var server = cluster.GetServerByHostName(hostName);
                    RequestServerStreamingMap(server);
                });
            }
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
                    Monitor.Wait(_gate, millisecondsToWait);

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

            CouchbaseClient client;
            try
            {
                client = new CouchbaseClient(bucketName, servers.ToArray());
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

        private void OnDisconnection(string hostName, IEnumerable<MemcachedCommand> pendingSends, IEnumerable<MemcachedCommand> pendingReceives)
        {
            NotifyCommands(pendingReceives, ResponseStatus.DisconnectionOccuredBeforeOperationCouldBeSent);

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

            TryToReconnectServerAndUpdateClusterIfServerStillExists(hostName);
        }

        private void TryToReconnectServerAndUpdateClusterIfServerStillExists(string hostName)
        {
            while (!GetHasQuit())
            {
                var serverWithDisconnectedMemcachedClient = _cluster.GetServerByHostName(hostName);

                if (serverWithDisconnectedMemcachedClient == null)
                    return;

                const int millisecondsToWaitBetweenReconnectAttempts = 1000;
                try
                {
                    TcpClient client = new TcpClient(hostName, serverWithDisconnectedMemcachedClient.Port);
                    var ar = client.BeginConnect(serverWithDisconnectedMemcachedClient.HostName, serverWithDisconnectedMemcachedClient.Port, result =>
                    {
                        if (result.CompletedSynchronously)
                            return;

                        try
                        {
                            client.EndConnect(result);

                            OnReconnect(hostName, client);
                        }
                        catch
                        {
                            //Wait and try again.
                            Thread.Sleep(millisecondsToWaitBetweenReconnectAttempts);
                            TryToReconnectServerAndUpdateClusterIfServerStillExists(hostName);
                        }
                    }, null);

                    if (!ar.CompletedSynchronously)
                        return;

                    OnReconnect(hostName, client);
                    return;
                }
                catch
                {
                    //Wait and try again.
                    Thread.Sleep(millisecondsToWaitBetweenReconnectAttempts);
                }
            }
        }

        private void OnReconnect(string hostName, TcpClient tcpClient)
        {
            lock (_gate)
            {
                var cluster = _cluster;

                //Update the cluster after we have successfully reconnected.
                var newCluster = cluster.Clone();
                var serverInNewCluster = newCluster.GetServerByHostName(hostName);

                if (serverInNewCluster == null || !_hasQuit)
                {
                    try
                    {
                        tcpClient.GetStream().Close();
                        tcpClient.Close();
                    }
                    catch
                    {
                    }
                    return;
                }

                MemcachedClient newMemcachedClient = new MemcachedClient(serverInNewCluster.HostName, serverInNewCluster.Port);

                newMemcachedClient.OnDisconnected += OnDisconnection;
                newMemcachedClient.OnRecoverableError += OnPossiblyRecoverableMemcachedError;
                newMemcachedClient.Connect(tcpClient);

                serverInNewCluster.MemcachedClient = newMemcachedClient;

                _cluster = newCluster;
            }
        }

        private void OnPossiblyRecoverableMemcachedError(string hostName, MemcachedCommand command)
        {
            var cluster = _cluster;

            switch (command.ResponseStatus)
            {
                case ResponseStatus.VbucketBelongsToAnotherServer:
                    OnWrongVBucketForServer(hostName, command);
                    break;
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    SendCommand(cluster, command); //Retry
                    break;
                default:
                    throw new Exception("Should be impossible to get here...");
            }
        }

        private void OnWrongVBucketForServer(string hostNameOfLastServerTriedToSendTo, MemcachedCommand command)
        {
            Cluster cluster = _cluster;

            var fastForwardedServers = cluster.GetFastForwardedServersForVBucket(command.VBucketId);

            if (!TrySendCommand(fastForwardedServers, command))
            {
                SendByLinearlyPollingServers(cluster, hostNameOfLastServerTriedToSendTo, command);
                return;
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

                indexOfServerToTry = MathUtils.CircularIncrement(indexOfServerToTry, cluster.Servers.Count);
            }

            command.NotifyComplete(ResponseStatus.DisconnectionOccuredWhileOperationWaitingToBeSent);
        }

        private static int GetIndexOfNextServerInCluster(Cluster cluster, string hostNameOfLastServerTriedToSendTo)
        {
            var indexOfLastServerTriedToSendTo = cluster.Servers.FindIndex(x => x.HostName == hostNameOfLastServerTriedToSendTo);

            int indexOfServerToTry = indexOfLastServerTriedToSendTo == -1 ? 0 : MathUtils.CircularIncrement(indexOfLastServerTriedToSendTo, cluster.Servers.Count);
            return indexOfServerToTry;
        }

        public View GetView(string document, string name)
        {
            return new View(this, document, name);
        }

        public void ExecuteHttpQuery(UriBuilder uriBuilder, string startOrEndKeyHint, Action<ResponseStatus, string, object> callback, object state)
        {
            Cluster cluster = _cluster;

            HttpCommand httpCommand = new HttpCommand(uriBuilder, state, callback);

            TrySendToviewRequestToFirstAvailableServer(cluster, httpCommand, startOrEndKeyHint);
        }

        private void TrySendToviewRequestToFirstAvailableServer(Cluster cluster, HttpCommand httpCommand, string keyHintForSelectingServer)
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

            httpCommand.NotifyComplete(ResponseStatus.DisconnectionOccuredBeforeOperationCouldBeSent);
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
                command.NotifyComplete(ResponseStatus.DisconnectionOccuredBeforeOperationCouldBeSent);
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
            bool hasQuit;
            lock (_gate)
            {
                hasQuit = _hasQuit;
            }
            return hasQuit;
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
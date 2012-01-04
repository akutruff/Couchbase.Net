using System;
using System.Collections.Generic;
using System.Linq;
using Json;
using System.Collections;

namespace FastCouch
{
    //Treat this as an immutable object.
    internal class Cluster
    {
        private List<List<Server>> _vBucketToServerMap;
        private List<List<Server>> _fastForwardVBucketToServerMap = new List<List<Server>>();
        
        private List<List<int>> _vBucketToServerMapIndices;
        private List<List<int>> _fastForwardVBucketToServerMapIndices;
        
        public List<Server> Servers { get; private set; }
        
        private Dictionary<string, Server> _hostNameToServer = new Dictionary<string, Server>();

        public Cluster()
        {
            this.Servers = new List<Server>();
        }

        public void SetVBucketToServerMap(List<List<int>> vBucketMap)
        {
            _vBucketToServerMapIndices = vBucketMap;
            _vBucketToServerMap = GetVBucketToServerMap(vBucketMap);
        }

        public void SetFastForwardVBucketToServerMap(List<List<int>> fastForwardVBucketMap)
        {
            _fastForwardVBucketToServerMapIndices = fastForwardVBucketMap;
            _fastForwardVBucketToServerMap = GetVBucketToServerMap(fastForwardVBucketMap);
        }

        private List<List<Server>> GetVBucketToServerMap(List<List<int>> vBucketMap)
        {
            List<List<Server>> vBucketToServerMap = new List<List<Server>>(vBucketMap.Count);

            for (int iVBucket = 0; iVBucket < vBucketMap.Count; iVBucket++)
            {
                var serverIndicesForVBucket = vBucketMap[iVBucket];
                var serversForVBucket = new List<Server>();
                vBucketToServerMap.Add(serversForVBucket);

                for (int i = 0; i < serverIndicesForVBucket.Count; i++)
                {
                    var serverIndex = serverIndicesForVBucket[i];
                    if (serverIndex >= 0)
                    {
                        var server = Servers[serverIndex];

                        serversForVBucket.Add(server);
                    }
                }
            }

            return vBucketToServerMap;
        }

        public void AddServer(Server server)
        {
            Servers.Add(server);
            _hostNameToServer[server.HostName] = server;
        }

        public void ConnectMemcachedClients(
            Cluster existingCluster, 
            Action<string, MemcachedCommand> onRecoverableError, 
            Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> onDisconnected,
            Action<string, HttpCommand> onHttpFailure)
        {
            var hostNameToExistingServer = existingCluster.Servers.ToDictionary(x => x.HostName);

            foreach (var server in Servers)
            {
                Server existingServer;
                if (hostNameToExistingServer.TryGetValue(server.HostName, out existingServer))
                {
                    server.MemcachedClient = existingServer.MemcachedClient;
                    server.HttpClient = existingServer.HttpClient;

                    hostNameToExistingServer.Remove(server.HostName);
                }

                if (server.MemcachedClient == null)
                {
                    server.Connect(onRecoverableError, onDisconnected, onHttpFailure);
                }
            }

            var existingServersThatAreNoLongerInTheCluster = hostNameToExistingServer.Values;

            foreach (var serverToDispose in existingServersThatAreNoLongerInTheCluster)
            {
                serverToDispose.Dispose();
            }
        }

        public int GetVBucket(string key)
        {
            return VBucketCalculator.GetId(key, _vBucketToServerMap.Count);
        }

        public Server GetServer(string key, out int vbucketId)
        {
            vbucketId = VBucketCalculator.GetId(key, _vBucketToServerMap.Count);

            var server = _vBucketToServerMap[vbucketId][0];

            return server;
        }

        public List<Server> GetServersForVBucket(int vbucketId)
        {
            return _vBucketToServerMap[vbucketId];
        }

        public List<Server> GetFastForwardedServersForVBucket(int vbucketId)
        {
            if (_fastForwardVBucketToServerMap == null)
            {
                return null;
            }

            return _fastForwardVBucketToServerMap[vbucketId];
        }

        public Server GetServerByHostName(string hostName)
        {
            Server server = null;
            _hostNameToServer.TryGetValue(hostName, out server);
            return server;
        }

        public Cluster Clone()
        {
            Cluster newCluster = new Cluster();
            
            newCluster.Servers.AddRange(Servers.Select(x => x.Clone()));
            
            if (_vBucketToServerMapIndices != null)
            {
                newCluster.SetVBucketToServerMap(DeepCopy(_vBucketToServerMapIndices));
            }            

            if (_fastForwardVBucketToServerMapIndices != null)
            {
                newCluster.SetFastForwardVBucketToServerMap(DeepCopy(_fastForwardVBucketToServerMapIndices));
            }

            newCluster._hostNameToServer = new Dictionary<string, Server>(_hostNameToServer);
           
            return newCluster;
        }

        private List<List<T>> DeepCopy<T>(List<List<T>> list)
        {
            return list.Select(x => x.ToList()).ToList();
        }
    }
}

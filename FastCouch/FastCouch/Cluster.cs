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
        
        private Dictionary<string, Server> _idToServer = new Dictionary<string, Server>();

        public Cluster()
        {
            this.Servers = new List<Server>();
        }

        public bool IsEquivalentConfiguration(Cluster otherCluster)
        {
            if (this.Servers.Count != otherCluster.Servers.Count)
            {
                return false;
            }

            var knownServersInThisCluster = new HashSet<string>(_idToServer.Keys);

            foreach (var otherClusterServerId in otherCluster._idToServer.Keys)
            {
                if (!knownServersInThisCluster.Contains(otherClusterServerId))
                {
                    return false;
                }
            }

            bool doVBucketMapsMatch = AreMapsEquivalent(this.Servers, _vBucketToServerMapIndices, otherCluster.Servers, otherCluster._vBucketToServerMapIndices) &&
                                      AreMapsEquivalent(this.Servers, _fastForwardVBucketToServerMapIndices, otherCluster.Servers, otherCluster._fastForwardVBucketToServerMapIndices);

            return doVBucketMapsMatch;
        }

        private static bool AreMapsEquivalent(List<Server> clusterOneServers, List<List<int>> clusterOneMapping, List<Server> clusterTwoServers, List<List<int>> clusterTwoMapping)
        {
            if (clusterOneMapping.Count != clusterTwoMapping.Count)
            {
                return false;
            }

            for (int i = 0; i < clusterOneMapping.Count; i++)
            {
                var sublistOne = clusterOneMapping[i];
                var sublistTwo = clusterTwoMapping[i];

                if (sublistOne.Count != sublistTwo.Count)
                {
                    return false;
                }

                for (int j = 0; j < sublistOne.Count; j++)
                {
                    int clusterOneServerIndex = sublistOne[j];
                    int clusterTwoServerIndex = sublistTwo[j];

                    if (clusterOneServerIndex == -1 || clusterTwoServerIndex == -1)
                    {

                    	if (clusterOneServerIndex != clusterTwoServerIndex)
                        {
                        	return false;
                        }
                    }
                    else if (clusterOneServers[clusterOneServerIndex].Id != clusterTwoServers[clusterTwoServerIndex].Id)
                    {
                        return false;
                    }
                }
            }

            return false;
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
            _idToServer[server.Id] = server;
        }

        public void ConfigureClusterFromExistingClusterAndCommitAnyConnectionChanges(
            Cluster existingCluster, 
            Action<string, MemcachedCommand> onRecoverableError, 
            Action<string, IEnumerable<MemcachedCommand>, IEnumerable<MemcachedCommand>> onDisconnected,
            Action<string, HttpCommand> onHttpFailure)
        {
            var serverIdToExistingServer = existingCluster.Servers.ToDictionary(x => x.Id);

            foreach (var server in Servers)
            {
                Server existingServer;
                if (serverIdToExistingServer.TryGetValue(server.Id, out existingServer))
                {
                    server.MemcachedClient = existingServer.MemcachedClient;
                    server.ViewHttpClient = existingServer.ViewHttpClient;
                    server.StreamingHttpClient = existingServer.StreamingHttpClient;

                    serverIdToExistingServer.Remove(server.Id);
                }

                server.Connect(onRecoverableError, onDisconnected, onHttpFailure);
            }

            var existingServersThatAreNoLongerInTheCluster = serverIdToExistingServer.Values;

            foreach (var serverNoLongerReportedInCluster in existingServersThatAreNoLongerInTheCluster)
            {
                serverNoLongerReportedInCluster.Dispose();
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
            if (vbucketId < 0 || vbucketId >= _fastForwardVBucketToServerMap.Count)
            {
                return null;
            }

            return _fastForwardVBucketToServerMap[vbucketId];
        }

        public Server GetServerById(string id)
        {
            Server server = null;
            _idToServer.TryGetValue(id, out server);
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

            newCluster._idToServer = newCluster.Servers.ToDictionary(x => x.Id);
           
            return newCluster;
        }

        private List<List<T>> DeepCopy<T>(List<List<T>> list)
        {
            return list.Select(x => x.ToList()).ToList();
        }
    }
}

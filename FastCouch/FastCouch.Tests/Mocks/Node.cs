using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch.Tests.Mocks
{
    public class Node : IDisposable
    {
        private StreamingClusterDataService _streamingService;
        private MemcachedServer _memcachedServer;

        public string Host { get; private set; }
        public int StreamingPort { get; private set; }
        public int MemcachedPort { get; private set; }
        public int ApiPort { get; private set; }

        public List<int> VBuckets { get; set; }
        public List<int> Replicas { get; set; }
        private object _gate = new object();

        public int Index { get; set; }

        public Node(string host, int nodeId, int index, int memcachedPort, int streamingPort, int apiPort)
        {
            this.Host = host;
            _streamingService = new StreamingClusterDataService(host, streamingPort);
            _memcachedServer = new MemcachedServer(host, memcachedPort);

            this.Index = index;

            this.StreamingPort = streamingPort;
            this.MemcachedPort = memcachedPort;
            this.ApiPort = apiPort;
        }

        public void Start()
        {
            _streamingService.Start();
        }

        public void UpdateCluster(List<Node> nodes, List<List<int>> vBucketMap, int replicationCount)
        {
            string clusterData;

            UpdateMyAssignedVBuckets(vBucketMap);

            clusterData = "{\"name\":\"default\",\"bucketType\":\"membase\",\"authType\":\"sasl\",\"saslPassword\":\"\",\"proxyPort\":0,\"uri\":\"/pools/default/buckets/default\",\"streamingUri\":\"/pools/default/bucketsStreaming/default\",\"flushCacheUri\":\"/pools/default/buckets/default/controller/doFlush\",\"nodes\":[" +
                   nodes.Aggregate(string.Empty, (previous, current) => previous + ((string.IsNullOrEmpty(previous) ? string.Empty : ",") + GetNodeDescription(current))) +
                "],\"stats\":{\"uri\":\"/pools/default/buckets/default/stats\",\"directoryURI\":\"/pools/default/buckets/default/statsDirectory\",\"nodeStatsListURI\":\"/pools/default/buckets/default/nodes\"},\"nodeLocator\":\"vbucket\",\"autoCompactionSettings\":false,\"vBucketServerMap\":{\"hashAlgorithm\":\"CRC\",\"numReplicas\":" +
                replicationCount +
                ",\"serverList\":[" +
                nodes.Select(x => "\"" + x.Host + ":" + x.MemcachedPort + "\"").Aggregate((previous, current) => previous + (string.IsNullOrEmpty(previous) ? string.Empty : ",") + current) +
                "],\"vBucketMap\":[" +
                vBucketMap.Select(x => "[" + x.Aggregate(string.Empty, (previous, current) => previous + (string.IsNullOrEmpty(previous) ? string.Empty : ",") + current) + "]").Aggregate((previous, current) => previous + (string.IsNullOrEmpty(previous) ? string.Empty : ",") + current) +
                "]},\"bucketCapabilitiesVer\":\"sync-1.0\",\"bucketCapabilities\":[\"touch\",\"sync\",\"couchapi\"]}\n\n\n\n";

            _streamingService.UpdateCluster(clusterData);
        }

        private void UpdateMyAssignedVBuckets(List<List<int>> vBucketMap)
        {
            List<int> vBuckets = new List<int>();
            List<int> replicas = new List<int>();

            for (int iVbucket = 0; iVbucket < vBucketMap.Count; iVbucket++)
            {
                var serversForVbucket = vBucketMap[iVbucket];

                if (serversForVbucket[0] == this.Index)
                {
                    vBuckets.Add(iVbucket);
                }

                for (int j = 1; j < serversForVbucket.Count; j++)
                {
                    if (serversForVbucket[j] == this.Index)
                    {
                        replicas.Add(iVbucket);
                    }
                }
            }

            lock (_gate)
            {
                this.VBuckets = VBuckets;
                this.Replicas = replicas;

                _memcachedServer.SetVbuckets(vBuckets);
            }
        }

        private static string GetNodeDescription(Node node)
        {
            var description = "{\"couchApiBase\":\"";
            description +=
                    "http://" + node.Host + ":" + node.ApiPort + "/default" +
                    "\",\"replication\":0.0,\"clusterMembership\":\"active\",\"status\":\"healthy\",\"thisNode\":true," +
                    "\"hostname\":\"" + node.Host + ":" + node.StreamingPort +
                    "\",\"clusterCompatibility\":1,\"version\":\"2.0.0r-388-gf35126e-community\",\"os\":\"windows\",\"ports\":{\"proxy\":11211,\"direct\":" + node.MemcachedPort + "}}";

            return description;
        }


        public void Dispose()
        {
            _streamingService.Dispose();
        }
    }
}

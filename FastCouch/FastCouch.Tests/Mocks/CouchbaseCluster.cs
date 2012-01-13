using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace FastCouch.Tests.Mocks
{
    public class CouchbaseCluster : IDisposable
    {
        public string Host { get; private set; }

        private object _gate = new object();

        public int ReplicationCount { get; private set; }

        public List<Node> Nodes { get; private set; }
        public List<List<int>> VBucketMap { get; set; }
        public int VBucketCount { get; set; }

        public int LastNodeId { get; set; }

        Random _rand = new Random();

        public CouchbaseCluster(string host, int vBucketCount, int numberOfNodes, int replication)
        {
            this.VBucketMap = new List<List<int>>();
            this.VBucketCount = vBucketCount;

            Host = host;
            ReplicationCount = replication;
            this.Nodes = new List<Node>();

            for (int i = 0; i < numberOfNodes; i++)
            {
                AddNewNode();
            }
        }

        public void AddNewNode(Node newNode)
        {
            List<Node> nodes;
            List<List<int>> vBucketMap;
            lock (_gate)
            {
                this.Nodes.Add(newNode);
                nodes = this.Nodes.ToList();
                vBucketMap = this.VBucketMap.ToList();
            }

            Rebalance();

            newNode.Start();

            foreach (var node in nodes)
            {
                node.UpdateCluster(nodes, VBucketMap, this.ReplicationCount);
            }
        }

        public void Rebalance()
        {
            List<int> unassignedVBuckets = new List<int>(VBucketCount);
            List<List<int>> vBucketMap = new List<List<int>>(this.VBucketCount);

            for (int i = 0; i < this.VBucketCount; i++)
            {
                unassignedVBuckets.Add(i);
                vBucketMap.Add(new List<int>());
            }

            for (int i = 0; i < this.Nodes.Count; i++)
            {
                var node = this.Nodes[i];
                node.Index = i;

                node.VBuckets = new List<int>();
                node.Replicas = new List<int>();
            }

            var replicationMap = new List<int>();

            int nextNodeIndex = _rand.Next(this.Nodes.Count);
            for (int i = 0; i < this.VBucketCount; i++)
            {
                Node node = this.Nodes[nextNodeIndex];
                nextNodeIndex = MathUtils.CircularIncrement(nextNodeIndex, this.Nodes.Count);

                var vBucket = ConsumeRandomElementFromList(unassignedVBuckets);
                vBucketMap[vBucket].Add(node.Index);
            }

            if (this.Nodes.Count > 1)
            {
                int nextReplicaIndex = 0;
                for (int i = 0; i < this.Nodes.Count; i++)
                {
                    var node = this.Nodes[i];
                    Node replicaNode = null;
                    foreach (var vBucket in node.VBuckets)
                    {
                        for (int j = 0; j < this.ReplicationCount; j++)
                        {
                            nextReplicaIndex = MathUtils.CircularIncrement(nextReplicaIndex, this.Nodes.Count);
                            if (nextReplicaIndex == i)
                            {
                                nextReplicaIndex = MathUtils.CircularIncrement(nextReplicaIndex, this.Nodes.Count);
                            }

                            replicaNode = this.Nodes[nextReplicaIndex];
                            replicaNode.Replicas.Add(nextReplicaIndex);

                            vBucketMap[vBucket].Add(nextReplicaIndex);
                        }
                    }
                }
            }

            for (int i = 0; i < vBucketMap.Count; i++)
            {
                var items = vBucketMap[i];
                var missingReplicas = (this.ReplicationCount + 1) - items.Count;

                for (int j = 0; j < missingReplicas; j++)
                {
                    items.Add(-1);
                }
            }

            this.VBucketMap = vBucketMap;
        }

        private T ConsumeRandomElementFromList<T>(List<T> elements)
        {
            var itemIndex = _rand.Next(elements.Count);
            var item = elements[itemIndex];

            elements.RemoveAt(itemIndex);

            return item;
        }

        public void AddNewNode()
        {
            lock (_gate)
            {
                var nodeId = this.LastNodeId++;
                var portOffset = 2 * nodeId;
                //var node = new Node("node" + nodeId + this.Host, nodeId, this.Nodes.Count);
                var node = new Node(this.Host, nodeId, this.Nodes.Count, 11212 + portOffset, 8093 + portOffset, 8094 + portOffset);
                AddNewNode(node);
            }
        }

        public void Dispose()
        {
            foreach (var node in this.Nodes)
            {
                try
                {
                    node.Dispose();
                }
                catch
                { }
            }
        }
    }
}
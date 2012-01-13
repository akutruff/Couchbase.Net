using System;
using System.Collections.Generic;
using System.Linq;
using Json;

namespace FastCouch
{
    internal static class ClusterParser
    {
        internal static readonly IJsonParsingOperations<Cluster> Instance;

        internal class NodeDescription
        {
            public string HostName { get; set; }
            public int StreamingPort { get; set; }
            public int ApiPort { get; set; }
            public int MemcachedPort { get; set; }
        }

        static ClusterParser()
        {
            Instance = JsonParser.Create<Cluster>()
                .OnArray("nodes", (cluster, nodeDescriptions) =>
                    {
                        foreach (var item in nodeDescriptions)
                        {
                            var server = EnsureServer(cluster, item.HostName, item.MemcachedPort);
                            server.StreamingPort = item.StreamingPort;
                            server.ApiPort = item.ApiPort;
                        }

                    }, cluster => new NodeDescription(), JsonParser.Create<NodeDescription>()
                        .OnString("couchApiBase", (node, serverApiUri) =>
                        {
                            Uri uri = new Uri(serverApiUri);
                            node.ApiPort = uri.Port;
                        })
                        .OnString("hostname", (node, serverNameAndPort) =>
                        {
                            string hostName;
                            int streamingPort;

                            ParseHostnameAndPort(serverNameAndPort, out hostName, out streamingPort);
                            node.HostName = hostName;
                            node.StreamingPort = streamingPort;
                        })
                        .OnGroup("ports", (_, __) => { }, nodeDescription => nodeDescription, JsonParser.Create<NodeDescription>()
                            .OnInt("direct", (nodeDescription, memcachedPort) =>
                            {
                                nodeDescription.MemcachedPort = memcachedPort;
                            })))
                .OnGroup<Cluster>("vBucketServerMap", (cluster, value) => { }, cluster => cluster, JsonParser.Create<Cluster>()
                    .OnArray("serverList", (cluster, serverNameAndPorts) =>
                    {
                        for (int i = 0; i < serverNameAndPorts.Count; i++)
                        {
                            string hostName;
                            int memcachedPort;

                            ParseHostnameAndPort(serverNameAndPorts[i], out hostName, out memcachedPort);

                            var server = EnsureServer(cluster, hostName, memcachedPort);

                            server.MemcachedPort = memcachedPort;
                        }
                    }, JsonParser.StringParser)
                    .OnArray("vBucketMap", (cluster, value) => cluster.SetVBucketToServerMap(value), new JsonArrayParser<int>(JsonParser.IntParser))
                    .OnArray("vBucketMapForward", (cluster, value) => cluster.SetFastForwardVBucketToServerMap(value), new JsonArrayParser<int>(JsonParser.IntParser)));
        }


        public static void ParseHostnameAndPort(string hostNameAndPort, out string hostName, out int port)
        {
            var hostNameAndPortStrings = hostNameAndPort.Split(':');

            hostName = hostNameAndPortStrings[0];
            port = Int32.Parse(hostNameAndPortStrings[1]);
        }

        public static Server EnsureServer(Cluster cluster, string hostname, int memcachedPort)
        {
            var id = Server.GetId(hostname, memcachedPort);
            var server = cluster.GetServerById(id);
            if (server == null)
            {
                server = new Server(hostname, memcachedPort);
                cluster.AddServer(server);
            }

            return server;
        }
    }
}

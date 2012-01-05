using System;
using System.Collections.Generic;
using System.Linq;
using Json;

namespace FastCouch
{
    internal static class ClusterParser
    {
        internal static readonly IJsonParsingOperations<Cluster> Instance;

        static ClusterParser()
        {
            Instance = JsonParser.Create<Cluster>()
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
    }
}

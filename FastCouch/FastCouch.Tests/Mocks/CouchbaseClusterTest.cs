using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;

namespace FastCouch.Tests.Mocks
{
    [TestFixture]
    public class CouchbaseClusterTest
    {
        CouchbaseCluster _target;

        [SetUp]
        public void Setup()
        {
            _target = new CouchbaseCluster("localhost", 16, 1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            _target.Dispose();
        }

        [Test]
        public void Test()
        {
            var client = new WebClient();
            //var url = "http://node0.localhost:8091/";
            var url = "http://localhost:8091/";
            
            var request = (HttpWebRequest)HttpWebRequest.Create(url);
            var response = request.GetResponse();
            var stream = response.GetResponseStream();
            StringBuilder builder = new StringBuilder();

            var buffer = new byte[1024];
            var decoder = Encoding.UTF8.GetDecoder();

            object gate = new object();
            bool hasCompleted = false;

            AsyncPattern<Stream> readAsync = AsyncPattern.Create<Stream>(
                (strea, pattern) => strea.BeginRead(buffer, 0, buffer.Length, pattern.OnCompleted, null),
                (result) =>
                {
                    var bytesRead = stream.EndRead(result);
                    if (bytesRead > 0)
                    {
                        char[] decoded = new char[buffer.Length * 2];

                        int bytesUsed;
                        int charsUsed;
                        bool completed;
                        decoder.Convert(buffer, 0, bytesRead, decoded, 0, buffer.Length, false, out bytesUsed, out charsUsed, out completed);

                        builder.Append(decoded, 0, charsUsed);

                        if (builder.ToString().EndsWith("\n\n\n\n"))
                        {
                            lock (gate)
                            {
                                hasCompleted = true;
                                Monitor.Pulse(gate);
                            }

                            return null;
                        }
                    }
                    return stream;
                },
                (result, e) =>
                {
                    Console.WriteLine(e.ToString());
                    lock (gate)
                    {
                        hasCompleted = true;
                        Monitor.Pulse(gate);
                    }
                    return null;
                });

            readAsync.BeginAsync(stream);
            lock (gate)
            {
                while (!hasCompleted)
                {
                    Monitor.Wait(gate);
                }
            }
            Console.WriteLine(builder.ToString());
            //Console.WriteLine(result);
        }
    }
}

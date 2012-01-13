using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FastCouch.Tests.Mocks;
using System.Threading;

namespace FastCouch.Tests
{
    [TestFixture]
    public class CouchbaseClientScalingTests
    {
        private CouchbaseClient _target;
        private CouchbaseCluster _cluster;

        [SetUp]
        public void Setup()
        {
            _cluster = new CouchbaseCluster("localhost", 16, 1, 1);
            Thread.Sleep(1000);
            
        }
        
        private void ConnectCouchbaseClient()
        {
            _target = CouchbaseClient.Connect("default", Int32.MaxValue, _cluster.Nodes.Select(x => new Server(x.Host, x.MemcachedPort, x.StreamingPort, x.ApiPort)).ToArray());
        }

        [TearDown]
        public void TearDown()
        {
            Console.WriteLine("Disposing...");

            try
            {
                _target.Dispose();
            }
            catch
            { }

            try
            {
                _cluster.Dispose();
            }
            catch 
            {}
        }

        [Test]
        public void SimpleConnection()
        {
            ConnectCouchbaseClient();

            object gate = new object();
            bool wasCalled = false;
            _target.Get("Hello",
                (status, value, cas, state) =>
                {
                    Console.WriteLine("Get: " + status);
                    lock (gate)
                    {
                        wasCalled = true;
                        Monitor.Pulse(gate);
                    }
                },
                null);

            lock (gate)
            {
                while (!wasCalled)
                {
                    Monitor.Wait(gate);
                }
            }
        }

        [Test]
        public void AddAServer()
        {
            object gate = new object();

            _cluster.AddNewNode();

            //Thread.Sleep(1000);
            Thread.Sleep(1000);
            
            ConnectCouchbaseClient();

            Thread.Sleep(1000);
        }

        [Test]
        public void AddMultipleServers()
        {
            int maxServers = 6;
            for (int i = 0; i < maxServers; i++)
            {
                //Thread.Sleep(10);
                _cluster.AddNewNode();
            }

            Thread.Sleep(2000);

            ConnectCouchbaseClient();
        }


        [Test]
        public void AddMultipleServers_AndDoGets()
        {
            int maxServers = 6;
            for (int i = 0; i < maxServers; i++)
            {
                //Thread.Sleep(10);
                _cluster.AddNewNode();
            }

            Thread.Sleep(2000);

            ConnectCouchbaseClient();

            object gate = new object();

            int callCount = 0;
            const int iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                var key = i.ToString();

                _target.Get(key,
                    (status, value, cas, state) =>
                    {
                        int callId = Interlocked.Increment(ref callCount);
                        //if ((callId % iterations / 4) == 0)
                        {
                            Console.WriteLine(key + " " + status);
                        }

                        if (callId == iterations)
                        {
                            lock (gate)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    },
                    null);
            }

            lock (gate)
            {
                while (iterations != callCount)
                {
                    Monitor.Wait(gate);
                }
                Console.WriteLine(callCount);
            }
        }

        [Test]
        public void AddAServer_WhileLotsOfGets()
        {
            ConnectCouchbaseClient();

            Thread.Sleep(2000);

            object gate = new object();

            int callCount = 0;
            const int iterations = 1000;

            for (int i = 0; i < iterations; i++)
            {
                var key = i.ToString();

                _target.Get(key,
                    (status, value, cas, state) =>
                    {
                        int callId = Interlocked.Increment(ref callCount);
                        if ((callId % iterations / 4) == 0)
                        {
                            Console.WriteLine(key + " " + status);
                        }

                        if (callId == iterations)
                        {
                            lock (gate)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    },
                    null);
            }

            int maxServers = 6;
            for (int i = 0; i < maxServers; i++)
            {
                Thread.Sleep(10);
                _cluster.AddNewNode();
                //Thread.Sleep(1);
            }

            lock (gate)
            {
                while (iterations != callCount)
                {
                    Monitor.Wait(gate); 
                }
                Console.WriteLine(callCount);
            }

            Console.Out.Flush(); 
            Thread.Sleep(2000);
            Console.Out.Flush();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.Diagnostics;

namespace FastCouch.Tests
{
    [TestFixture]
    public class CouchbaseClientTest
    {
        private CouchbaseClient _target;

        [SetUp]
        public void Setup()
        {
            //_target = CouchbaseClient.Connect("default", 10000, "spec4");
            _target = CouchbaseClient.Connect("default", Int32.MaxValue, "spec4");
            //_target = CouchbaseClient.Connect("default", Int32.MaxValue, "192.168.1.4");
        }

        [TearDown]
        public void TearDown()
        {
            _target.Quit(); // just be nice and try to let the servers know that weare closing down.
            Thread.Sleep(250);
            _target.Dispose();
        }


        [Test]
        public void Get()
        {
            object gate = new object();
            int itemsCompleted = 0;

            var state = new object();

            const int iterations = 1;

            for (int i = 0; i < iterations; i++)
            {
                string receivedValue = null;
                ResponseStatus receivedStatus;
                object receivedState = null;

                _target.Get("Component00", (status, value, cas, stat) =>
                {
                    receivedStatus = status;
                    receivedValue = value;
                    receivedState = stat;
                    Console.WriteLine(status.ToString() + " " + value);
                    lock (gate)
                    {
                        Console.WriteLine(itemsCompleted);
                        if (++itemsCompleted == iterations)
                        {
                            Monitor.Pulse(gate);
                        }
                    }
                }, state);
            }

            lock (gate)
            {
                while (itemsCompleted < iterations)
                {
                    Monitor.Wait(gate);
                }
            }
        }


        [Test]
        public void Set()
        {
            object gate = new object();
            int itemsCompleted = 0;

            var state = new object();

            const int iterations = 1;

            for (int i = 0; i < iterations; i++)
            {
                string receivedValue = null;
                ResponseStatus receivedStatus;
                object receivedState = null;

                const string valueString = "{\"Str\":\"World\"}";

                _target.Set("Hello", valueString,
                    (status, value, cas, stat) =>
                    {
                        receivedStatus = status;
                        receivedValue = value;
                        receivedState = stat;
                        Console.WriteLine(status.ToString() + " " + value + " " + stat.ToString());
                        lock (gate)
                        {
                            if (++itemsCompleted == iterations)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    }, state);
            }

            lock (gate)
            {
                while (itemsCompleted < iterations)
                {
                    Monitor.Wait(gate);
                }
            }
        }

        [Test]
        public void FillUpWithForeignKeyedItems()
        {
            object gate = new object();
            int itemsCompleted = 0;

            var state = new object();

            const int items = 100;
            const int componentsPerItem = 5;
            
            Random rand = new Random();
            
            for (int i = 0; i < items; i++)
            {                
                string itemKey = "Item" + i;


                _target.Set(itemKey, "{\"Number\":" + i + "}",
                    (status, value, cas, stat) =>
                    {
                        Console.WriteLine(status.ToString() + " " + value + " " + stat.ToString());
                        lock (gate)
                        {
                            if (++itemsCompleted == items)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    }, state);

                for (int j = 0; j < componentsPerItem; j++)
                {
                    //var owningItem = "Item" + rand.Next(items);

                    string valueString = "{\"Type\":\"Component\",\"OwnerRef\":\"" + itemKey + "\",\"EntityRef\":" + j / 2 + "}";

                    _target.Set("Component" + i + j, valueString,
                        (status, value, cas, stat) =>
                        {
                            Console.WriteLine(status.ToString() + " " + value + " " + stat.ToString());
                            lock (gate)
                            {
                                if (++itemsCompleted == items)
                                {
                                    Monitor.Pulse(gate);
                                }
                            }
                        }, state);
                }
            }

            lock (gate)
            {
                while (itemsCompleted < items)
                {
                    Monitor.Wait(gate);
                }
            }
        }

        [Test]
        public void SetsAndGetsPerSecond()
        {
            object gate = new object();
            //int itemsCompleted = 0;
            int itemsCompleted = 0;
            var state = new object();

            var watch = Stopwatch.StartNew();
            const int iterations = 100000;

            const int totalItems = 2 * iterations;

            const string valueString = "{\"Str\":\"World\"}";

            for (int i = 0; i < iterations; i++)
            {
                _target.Set("Hello", valueString,
                    (status, value, cas, stat) =>
                    {
                        if (Interlocked.Increment(ref itemsCompleted) == totalItems)
                        {
                            lock (gate)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    }, state);

                _target.Get("Hello", (status, value, cas, stat) =>
                    {
                        if (Interlocked.Increment(ref itemsCompleted) == totalItems)
                        {
                            lock (gate)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    }, state);
            }
            var timeForRequestsToBePosted = watch.Elapsed.TotalSeconds;

            lock (gate)
            {
                while (itemsCompleted < totalItems)
                {
                    Monitor.Wait(gate);
                    //Console.WriteLine(itemsCompleted);
                }
            }
            watch.Stop();

            var totalTime = watch.Elapsed.TotalSeconds;
            Console.WriteLine("total time: " + totalTime);
            Console.WriteLine("requests time: " + timeForRequestsToBePosted);
            Console.WriteLine("requests/sec: " + totalItems / timeForRequestsToBePosted);
            Console.WriteLine("trips/sec: " + totalItems / totalTime);
        }

        [Test]
        [Explicit]
        public void BigValueTest()
        {
            object gate = new object();
            int setsCompleted = 0;

            var state = new object();

            StringBuilder builder = new StringBuilder();
            builder.Append("{\"Str\":\"");

            string item = "Andy";
            const int amountOfDataToSendInMegaBytes = 1;
            int timesToAppend = (amountOfDataToSendInMegaBytes * 1024 * 1024) / item.Length;

            for (int iItem = 0; iItem < timesToAppend; iItem++)
            {
                builder.Append(item);
            }

            builder.Append("\"}");

            string valueString = builder.ToString();

            const int iterations = 1;

            for (int i = 0; i < iterations; i++)
            {
                _target.Set("Hello", valueString,
                    (status, value, cas, stat) =>
                    {
                        Console.WriteLine(status.ToString() + " " + value + " " + stat.ToString());
                        lock (gate)
                        {
                            if (++setsCompleted == iterations)
                            {
                                Monitor.Pulse(gate);
                            }
                        }
                    }, state);
            }

            lock (gate)
            {
                while (setsCompleted < iterations)
                {
                    Monitor.Wait(gate);
                }
            }

            int getsCompleted = 0;

            for (int i = 0; i < iterations; i++)
            {
                _target.Get("Hello", (status, value, cas, stat) =>
                {
                    Console.WriteLine(status.ToString());
                    lock (gate)
                    {
                        if (++getsCompleted == iterations)
                        {
                            Assert.AreEqual(valueString, value);
                            Monitor.Pulse(gate);
                        }
                    }
                }, state);
            }

            lock (gate)
            {
                while (getsCompleted < iterations)
                {
                    Monitor.Wait(gate);
                }
            }
        }

        [Test]
        [Explicit]
        public void CheckForDisconnectTimeout()
        {
            Thread.Sleep(60000);
            Get();
        }

        [Test]
        public void Quit()
        {
            var state = new object();

            const int iterations = 1;

            for (int i = 0; i < iterations; i++)
            {
                _target.Quit();
            }
        }


        [Test]
        public void Delete()
        {
            _target.Delete("testKey", (status, val, cas, stat) => { }, null);
        }
    }
}
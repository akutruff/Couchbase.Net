using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Threading;
using System.Diagnostics;

namespace FastCouch.Tests
{
    [TestFixture]
    public class ViewTest
    {
        private CouchbaseClient _target;

        [SetUp]
        public void Setup()
        {
            //_target = CouchbaseClient.Connect("default", 10000, "spec4");
            _target = CouchbaseClient.Connect("default", Int32.MaxValue, "spec4");
            //_target = CouchbaseClient.Connect("default", Int32.MaxValue, "192.168.1.4");
        }


        [Test]
        public void Test()
        {
            object gate = new object();
            bool hasCompleted = false;

            var state = new object();

            var view = _target.GetView("EntityDocument", "References");
            view.Get((status, value, stat) =>
            {
                Console.WriteLine(status.ToString());
                lock (gate)
                {
                    Console.WriteLine(value);
                    hasCompleted = true;
                    Monitor.Pulse(gate);
                }
            },
            state,
            "Component_Owner_Item0");

            lock (gate)
            {
                while (!hasCompleted)
                {
                    Monitor.Wait(gate);
                }
            }
        }

        [Test]
        public void ManyIterations()
        {
            object gate = new object();

            int itemsCompleted = 1;
            var state = new object();

            var watch = Stopwatch.StartNew();
            const int iterations = 100;

            var view = _target.GetView("EntityDocument", "References");
            //var view = _target.GetView("DocumentOne", "Again");

            for (int i = 0; i < iterations; i++)
            {
                view.Get((status, value, stat) =>
                {
                    Console.WriteLine(value.ToString());

                    if (Interlocked.Increment(ref itemsCompleted) == iterations)
                    {
                        lock (gate)
                        {
                            Monitor.Pulse(gate);
                        }
                    }
                },
                state,
                //"Component" + (i % 20) + 6);
                "Component_Owner_Item" + i % 20);
            }

            lock (gate)
            {
                while (itemsCompleted < iterations)
                {
                    Monitor.Wait(gate);
                    //Console.WriteLine(itemsCompleted);
                }
            }
            watch.Stop();
            
            var elapsed = watch.Elapsed.TotalSeconds;
            Console.WriteLine("time " + elapsed);
            Console.WriteLine("gets/sec" + iterations/elapsed);
        }
    }
}

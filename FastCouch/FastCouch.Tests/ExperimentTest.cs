using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Threading;

namespace FastCouch.Tests
{
    [TestFixture]
    public class ExperimentTest
    {
        [Test]
        public void TimerTest()
        {
            const int timeToWait = 5000;
            bool wasCalled = false;
            object gate = new object();
            Timer timer = new Timer(stat =>
            {
                lock (gate)
                {
                    wasCalled = true;
                    Monitor.Pulse(gate);
                }
            }, null, timeToWait, Timeout.Infinite);

            WeakReference reference = new WeakReference(timer);
            timer = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.IsFalse(reference.IsAlive);
            lock (gate)
            {
                while (!wasCalled)
                {
                    bool signaledBeforeTimeout = Monitor.Wait(gate, 2 * timeToWait);
                    Assert.IsTrue(signaledBeforeTimeout);
                }
            }
        }
    }
}

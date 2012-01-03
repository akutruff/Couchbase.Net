using System;
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;
using System.Threading;
using System.Text;

namespace FastCouch.Tests
{
    [TestFixture]
    public class RequestStreamWriterTest
    {
        private RequestStreamWriter _target;

        [Test]
        public void TestInter()
        {
            //int foo = Int32.MaxValue;
            for (int i = Int32.MaxValue - 8; i <= Int32.MaxValue; i++)
            {
                uint newVariable = ((uint)i) % (uint)3;
                Console.WriteLine(i.ToString() + " " + newVariable);
            }
            //var newFoo = Interlocked.Increment(ref foo) % 3;
            //Console.WriteLine(newFoo);
        }

        [Test]
        public void Test()
        {
            object gate = new object();
            bool hasCompleted = false;
            List<byte> bytesRead = new List<byte>();
            _target = new RequestStreamWriter(
                new TestStream(
                    (buffer, offset, count) =>
                    {
                        int a = 0;
                        a++;
                    },
                    (buffer, offset, count) =>
                    {
                        lock (gate)
                        {
                            var bytes = new byte[count];
                            Array.Copy(buffer, offset, bytes, 0, count);
                            bytesRead.AddRange(bytes);

                            hasCompleted = bytesRead.Count >= 28;
                            Monitor.Pulse(gate);
                        }
                    }),
                command => null,
                () => { });


            object state = new object();
            GetCommand getCommand = new GetCommand(3, "key0", state, (status, value, cas, stat) => {});
            getCommand.SetVBucketId(17);

            _target.Send(getCommand);

            lock (gate)
            {
                while (!hasCompleted)
                {
                    Monitor.Wait(gate);
                }
            }

            var written = bytesRead.ToArray();
            Assert.AreEqual(28, written.Length);

            Assert.AreEqual(MagicBytes.RequestPacket, (MagicBytes)written[0]);
            Assert.AreEqual(Opcode.Get, (Opcode)written[1]);
            Assert.AreEqual(4, BitParser.ParseUShort(written, 2));
            Assert.AreEqual(0, written[4]);
            Assert.AreEqual(0, written[5]);
            Assert.AreEqual(17, BitParser.ParseUShort(written, 6));
            Assert.AreEqual(4, BitParser.ParseInt(written, 8));
            Assert.AreEqual(3, BitParser.ParseInt(written, 12));
            Assert.AreEqual(0, BitParser.ParseLong(written, 16));
            Assert.AreEqual("key0", Encoding.UTF8.GetString(written, 24, 4));
        }
    }
}
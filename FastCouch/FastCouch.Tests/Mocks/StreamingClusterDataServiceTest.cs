using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Threading;
using System.Net;

namespace FastCouch.Tests.Mocks
{
    [TestFixture]
    public class StreamingClusterDataServiceTest
    {
        private StreamingClusterDataService _target;

        [SetUp]
        public void Setup()
        {
            _target = new StreamingClusterDataService("localhost", 8080);
            _target.Start();
            Thread.Sleep(500);
        }

        [TearDown]
        public void TearDown()
        {
            _target.Dispose();
        }

        [Test]
        public void SimpleRequest()
        {
            WebClient client = new WebClient();
            var url = "http://localhost:8080/";

            var result = client.DownloadString(url);
            Console.WriteLine(result);
        }
    }
}

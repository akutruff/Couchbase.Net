using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Json.Tests
{
    [TestFixture]
    public class JsonWritingTests
    {
        [Test]
        public void WriteEmpty()
        {
            var writer = JsonWriter.Create<object>();
            Assert.AreEqual("{}", writer.ToString(new object()));
        }

        [Test]
        public void WriteOneField()
        {
            var writer = JsonWriter.Create<object>().Write("one", obj => 1);
            Assert.AreEqual(@"{""one"":1}", writer.ToString(new object()));
        }

        [Test]
        public void WriteComplicated()
        {
            object root = new object();
            string sub = "subGroup";

            var writer = JsonWriter.Create<object>()
                .Write("one", obj => { Assert.AreEqual(root, obj); return 1; })
                .Write("two", obj => "SomeUnicodeಠInString")
                .Write("three", obj => 1.0f)
                .Write("four", obj => new List<double> { 1.7, -3.4 }, JsonWriter.DoubleWriter)
                .Write("five", obj => sub, JsonWriter.Create<string>()
                    .Write("subOne", subStr => { Assert.AreEqual(sub, subStr); return Int32.MinValue; })
                    .Write("subTwo", subStr => new Guid("04363959-e582-4087-89fa-e9381130a875")))
                .Write("six", obj => 0xFFFF)
                .Write("Seven", obj => true)
                .Write("Eight", obj => long.MaxValue)
                .Write("nine", obj => @"\/'""" + '\n' + '\r' + '\f');

            var value = writer.ToString(root);
            Assert.AreEqual(@"{""one"":1,""two"":""SomeUnicode\u0ca0InString"",""three"":1,""four"":[1.7,-3.4],""five"":{""subOne"":-2147483648,""subTwo"":""04363959-e582-4087-89fa-e9381130a875""},""six"":65535,""Seven"":true,""Eight"":9223372036854775807,""nine"":""\\\/\'\""\n\r\f""}", value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Json.Tests
{
    [TestFixture]
    public class JsonParserTests
    {
        private static readonly string TestJson = @"
{
  ""stringField"": ""Bob"", 
  ""intField"": 91321, 
  ""negativeField"": -8, 
  ""boolFieldTrue"" : true
  ""boolFieldFalse"" : false
  ""stringArray"": [
    ""Jim"", 
    ""Joe"", 
  ], 
  ""intArray"": [
    1, 
    6, 
  ], 
  ""escapedQuotes"": ""\""value\"""", 
  ""arrayOfArrays"" : [ [0, 1], [2, 3] ],
  ""subGroup"" : {
        ""subField"" : 321
  }
}";

        public class Parsed
        {
            public string StringField { get; set; }
            public int IntField { get; set; }
            public int NegativeField { get; set; }
            public bool BoolFieldTrue { get; set; }
            public bool BoolFieldFalse { get; set; }
            public List<string> StringArray { get; set; }
            public List<int> IntArray { get; set; }
            public string EscapedQuotes { get; set; }
            public List<List<int>> ArrayOfArrays { get; set; }

            private SubGroup _subGroup;
            public SubGroup SubGroup
            {
                get
                {
                    if (_subGroup == null)
                    {
                        _subGroup = new SubGroup();
                    }
                    return _subGroup;
                }
            }
        }

        public class SubGroup
        {
            public int SubField { get; set; }
        }

        private static IJsonParsingOperations<Parsed> CreateFullParser()
        {
            return JsonParser.Create<Parsed>()
                .OnString("stringField", (parsed, value) => parsed.StringField = value)
                .OnInt("intField", (parsed, value) => parsed.IntField = value)
                .OnInt("negativeField", (parsed, value) => parsed.NegativeField = value)
                .OnBool("boolFieldTrue", (parsed, value) => parsed.BoolFieldTrue = value)
                .OnBool("boolFieldFalse", (parsed, value) => parsed.BoolFieldFalse = value)
                .OnString("escapedQuotes", (parsed, value) => parsed.EscapedQuotes = value)
                .OnArray("stringArray", (parsed, value) => parsed.StringArray = value, JsonParser.StringParser)
                .OnArray("arrayOfArrays", (parsed, value) => parsed.ArrayOfArrays = value, new JsonArrayParser<int>(JsonParser.IntParser))
                .OnGroup("subGroup", (parsed, value) => { }, parsed => parsed.SubGroup, JsonParser.Create<SubGroup>().OnInt("subField", (subParsed, value) => subParsed.SubField = value));
        }

        [Test]
        public void Test()
        {
            var parser = CreateFullParser();
            var result = new Parsed();

            parser.Parse(TestJson, result);

            Assert.AreEqual("Bob", result.StringField);
            Assert.AreEqual(91321, result.IntField);
            Assert.AreEqual(-8, result.NegativeField);
            Assert.IsTrue(result.BoolFieldTrue);
            Assert.IsFalse(result.BoolFieldFalse);
            Assert.AreEqual("\"value\"", result.EscapedQuotes);
            
            CollectionAssert.AreEquivalent(new List<string> { "Jim", "Joe" }, result.StringArray);
            
            CollectionAssert.AreEquivalent(new List<int> { 0, 1 }, result.ArrayOfArrays[0]);
            CollectionAssert.AreEquivalent(new List<int> { 2, 3 }, result.ArrayOfArrays[1]);

            Assert.AreEqual(321, result.SubGroup.SubField);
        }

        private class ParsedGuid
        {
            public Guid Guid { get; set; }
        }
        
        [Test]
        public void GuidTest()
        {
            var parser = JsonParser.Create<ParsedGuid>().OnGuid("guidField", (parsed, value) => parsed.Guid = value);
            var parsedGuid = new ParsedGuid();
            var guid = new Guid("0b7fd117-8ae6-4157-9f3b-db3adc91f106");

            parser.Parse("{ \"guidField\" : \"0b7fd117-8ae6-4157-9f3b-db3adc91f106\"}", parsedGuid);

            Assert.AreEqual(guid, parsedGuid.Guid);
        }

        [Test]
        public void GuidTest_NoDashes()
        {
            var parser = JsonParser.Create<ParsedGuid>().OnGuid("guidField", (parsed, value) => parsed.Guid = value);
            var parsedGuid = new ParsedGuid();
            var guid = new Guid("0b7fd117-8ae6-4157-9f3b-db3adc91f106");

            parser.Parse("{ \"guidField\" : \"0b7fd1178ae641579f3bdb3adc91f106\"}", parsedGuid);

            Assert.AreEqual(guid, parsedGuid.Guid);
        }

        private class ParsedString
        {
            public string String { get; set; }
        }
        
        [Test]
        public void UnicodeEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();
            parser.Parse("{ \"stringField\" : \"\\u0062\"}", parsedString);

            Assert.AreEqual("b", parsedString.String);
        }
        [Test]
        public void UnicodeEscapes_WithTrailingValue()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();
            parser.Parse("{ \"stringField\" : \"\\u0063blah\"}", parsedString);

            Assert.AreEqual("cblah", parsedString.String);
        }

        [Test]
        public void NewLineEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();
            
            parser.Parse("{ \"stringField\" : \"a\\nb\"}", parsedString); 
            Assert.AreEqual("a\nb", parsedString.String);
        }

        [Test]
        public void LineFeedEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\rb\"}", parsedString);
            Assert.AreEqual("a\rb", parsedString.String);
        }

        [Test]
        public void TabEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\tb\"}", parsedString);
            Assert.AreEqual("a\tb", parsedString.String);
        }

        [Test]
        public void FormFeedEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\fb\"}", parsedString);
            Assert.AreEqual("a\fb", parsedString.String);
        }
       
        [Test]
        public void BackspaceEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\bb\"}", parsedString);
            Assert.AreEqual("a\bb", parsedString.String);
        }

        [Test]
        public void BackslashEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\\\b\"}", parsedString);
            Assert.AreEqual("a\\b", parsedString.String);
        }

        [Test]
        public void ForwardslashEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\/b\"}", parsedString);
            Assert.AreEqual("a/b", parsedString.String);
        }

        [Test]
        public void QuoteEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\'b\"}", parsedString);
            Assert.AreEqual("a'b", parsedString.String);
        }

        [Test]
        public void DoublequoteEscapes()
        {
            var parser = JsonParser.Create<ParsedString>().OnString("stringField", (parsed, value) => parsed.String = value);
            var parsedString = new ParsedString();

            parser.Parse("{ \"stringField\" : \"a\\\"b\"}", parsedString);
            Assert.AreEqual("a\"b", parsedString.String);
        }

        [Test]
        public void ExampleCouchbaseMap()
        {
            string json = "{\"name\":\"default\",\"bucketType\":\"membase\",\"authType\":\"sasl\",\"saslPassword\":\"\",\"proxyPort\":0,\"uri\":\"/pools/default/buckets/default\",\"streamingUri\":\"/pools/default/bucketsStreaming/default\",\"flushCacheUri\":\"/pools/default/buckets/default/controller/doFlush\",\"nodes\":[{\"couchApiBase\":\"http://192.168.1.9:8092/default\",\"replication\":0.0,\"clusterMembership\":\"active\",\"status\":\"healthy\",\"thisNode\":true,\"hostname\":\"192.168.1.9:8091\",\"clusterCompatibility\":1,\"version\":\"2.0.0r-388-gf35126e-community\",\"os\":\"windows\",\"ports\":{\"proxy\":11211,\"direct\":11210}}],\"stats\":{\"uri\":\"/pools/default/buckets/default/stats\",\"directoryURI\":\"/pools/default/buckets/default/statsDirectory\",\"nodeStatsListURI\":\"/pools/default/buckets/default/nodes\"},\"nodeLocator\":\"vbucket\",\"autoCompactionSettings\":false,\"vBucketServerMap\":{\"hashAlgorithm\":\"CRC\",\"numReplicas\":1,\"serverList\":[\"192.168.1.9:11210\"],\"vBucketMap\":[[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1],[0,-1]]},\"bucketCapabilitiesVer\":\"sync-1.0\",\"bucketCapabilities\":[\"touch\",\"sync\",\"couchapi\"]}";

        }
    }
}

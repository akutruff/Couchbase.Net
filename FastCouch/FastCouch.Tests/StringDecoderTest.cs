using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using System.Text;

namespace FastCouch.Tests
{
    [TestFixture]
    public class StringDecoderTest
    {
        [Test]
        public unsafe void EncoderTest()
        {
            //string str = "abcdefghijklmnop\u0135";
            string str = "p\u0135";

            var encoder = Encoding.UTF8.GetEncoder();
            var buffer = new byte[2];

            List<byte> encoded = new List<byte>();
            int currentCharacter = 0;
            int currentByte = 0;

            while (currentCharacter < str.Length)
            {
                fixed (char* pString = str)
                fixed (byte* pBuffer = buffer)
                {
                    var charactersLeft = str.Length - currentCharacter;

                    int charsUsed;
                    int bytesUsed;
                    bool completed;
                    encoder.Convert(pString + currentCharacter, charactersLeft, pBuffer + currentByte, buffer.Length, false, out charsUsed, out bytesUsed, out completed);

                    currentCharacter += charsUsed;
                    encoded.AddRange(buffer.Take(bytesUsed));
                    Console.WriteLine("bytes: " + bytesUsed + " chars: " + charsUsed);
                }
            }

            var encodedString = Encoding.UTF8.GetString(encoded.ToArray());
            Console.WriteLine(encodedString);
            Assert.AreEqual(str, encodedString);
        }

        [Test]
        public unsafe void DecoderTest()
        {
            //string str = "abcdefghijklmnop\u0135";
            string str = "\u0135\u0135\u0135\u0135\u0135";

            var decodeBuffer = new ArraySegment<char>(new char[3]);
            StringDecoder decoder = new StringDecoder(decodeBuffer);

            decoder.Decode(new ArraySegment<byte>(Encoding.UTF8.GetBytes(str)));

            var decodedString = decoder.ToString();
            Console.WriteLine(decodedString);
            Assert.AreEqual(str, decodedString);
        }

        [Test]
        public unsafe void DecoderMultipleMisalignedCallTest()
        {
            //string str = "abcdefghijklmnop\u0135";
            string str = "\u0135\u0136\u0137\u0138\u0139";

            var decodeBuffer = new ArraySegment<char>(new char[3]);
            StringDecoder decoder = new StringDecoder(decodeBuffer);

            byte[] bytes = Encoding.UTF8.GetBytes(str);
            decoder.Decode(new ArraySegment<byte>(bytes.Take(3).ToArray()));
            decoder.Decode(new ArraySegment<byte>(bytes.Skip(3).ToArray()));

            var decodedString = decoder.ToString();
            Console.WriteLine(decodedString);
            Assert.AreEqual(str, decodedString);
        }

        [Test]
        public unsafe void DecodeUntilMultipleMisalignedCallTest()
        {
            //string str = "abcdefghijklmnop\u0135";
            string str = "\u0135\n\u0136\u0137\u0138\u0139";

            var decodeBuffer = new ArraySegment<char>(new char[3]);
            StringDecoder decoder = new StringDecoder(decodeBuffer);

            byte[] bytes = Encoding.UTF8.GetBytes(str);
            
            string result;

            var bytesLeftOver = new ArraySegment<byte>(bytes.Take(4).ToArray());

            Assert.IsTrue(decoder.DecodeUntilUtf8Character(bytesLeftOver, '\n', out result, out bytesLeftOver));
            Assert.AreEqual("\u0135\n", result);
            Assert.AreEqual(1, bytesLeftOver.Count);

            Assert.IsFalse(decoder.DecodeUntilUtf8Character(bytesLeftOver, '\n', out result, out bytesLeftOver));
            Assert.IsNull(result);
            Assert.AreEqual(0, bytesLeftOver.Count);
        }

        [Test]
        public unsafe void DecodeUntil()
        {
            //string str = "abcdefghijklmnop\u0135";
            string str = "\u0135\n\u0136\u0137\n\u0138\u0139";

            var decodeBuffer = new ArraySegment<char>(new char[3]);
            StringDecoder decoder = new StringDecoder(decodeBuffer);

            byte[] bytes = Encoding.UTF8.GetBytes(str);


            var bytesLeftOver = new ArraySegment<byte>(bytes.Take(9).ToArray());

            string result;

            Assert.IsTrue(decoder.DecodeUntilUtf8Character(bytesLeftOver, '\n', out result, out bytesLeftOver));
            Assert.AreEqual("\u0135\n", result);
            Assert.AreEqual(6, bytesLeftOver.Count);

            Assert.IsTrue(decoder.DecodeUntilUtf8Character(bytesLeftOver, '\n', out result, out bytesLeftOver));
            Assert.AreEqual("\u0136\u0137\n", result);
            Assert.AreEqual(1, bytesLeftOver.Count);

            Assert.IsFalse(decoder.DecodeUntilUtf8Character(bytesLeftOver, '\n', out result, out bytesLeftOver));
            Assert.IsNull(result);
            Assert.AreEqual(0, bytesLeftOver.Count);
        }
    }
}

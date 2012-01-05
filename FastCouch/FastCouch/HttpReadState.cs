using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;

namespace FastCouch
{
    public struct HttpReadState
    {
        public ArraySegment<byte> Buffer;

        public HttpWebRequest WebRequest;
        public WebResponse WebResponse;
        public Stream Stream;
        public StringDecoder StringDecoder;

        public bool HasStillMoreBytesToRead;

        public HttpReadState(HttpWebRequest request)
        {
            WebRequest = request;
            WebResponse = null; 
            Stream = null;
            Buffer = new ArraySegment<byte>();
            StringDecoder = new StringDecoder();
            HasStillMoreBytesToRead = true;
        }
    }
}

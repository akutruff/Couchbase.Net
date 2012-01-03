using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;

namespace FastCouch
{
    public class HttpCommand
    {
        public object State { get; private set; }

        public ResponseStatus ResponseStatus { get; set; }
        
        public HttpReadState HttpReadState;

        public UriBuilder UriBuilder { get; private set; }

        public Action<ResponseStatus, string, object> OnComplete { get; private set; }

        public string Value { get; set; }
     
        public HttpCommand(UriBuilder builder, object state, Action<ResponseStatus, string, object> onComplete)
        {
            UriBuilder = builder;
            State = state;
            OnComplete = onComplete;
        }

        public void NotifyComplete()
        {
            OnComplete(this.ResponseStatus, this.Value, this.State);
        }
    }

    public struct HttpReadState
    {
        public ArraySegment<byte> Buffer;
        
        public HttpWebRequest WebRequest;
        public Stream Stream;
        public StringDecoder StringDecoder;

        public bool HasStillMoreBytesToRead;

        public HttpReadState(HttpWebRequest request)
        {
            WebRequest = request;
            Stream = null;
            Buffer = new ArraySegment<byte>();
            StringDecoder = new StringDecoder();
            HasStillMoreBytesToRead = true;
        }
    }
}
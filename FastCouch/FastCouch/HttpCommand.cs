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
        private static BufferPool<byte> BufferPool = new BufferPool<byte>(128, 2048);
                                                                                            
        public object State { get; private set; }

        public ResponseStatus ResponseStatus { get; set; }
        
        public HttpReadState HttpReadState;

        public int TimeoutInMilliseconds { get; private set; }
        
        public UriBuilder UriBuilder { get; private set; }

        public Action<ResponseStatus, string, object> OnComplete { get; private set; }

        public string Value { get; set; }

        internal bool HasBeenAborted { get; set; }

        public HttpCommand(UriBuilder builder, object state, Action<ResponseStatus, string, object> onComplete, int timeoutInMilliseconds = -1)
        {
            TimeoutInMilliseconds = timeoutInMilliseconds;
            UriBuilder = builder;
            State = state;
            OnComplete = onComplete;
        }

        public void SetHost(string hostName, int port)
        {
            UriBuilder.Host = hostName;
            UriBuilder.Port = port;
        }

        public void BeginRequest(HttpWebRequest request)
        {
            HttpReadState = new HttpReadState(request);

            if (TimeoutInMilliseconds != -1)
            {
                request.Timeout = TimeoutInMilliseconds;
            }
        }

        public void NotifyComplete(ResponseStatus responseStatus)
        {
            this.ResponseStatus = responseStatus;
            NotifyComplete();
        }

        public void OnGotResponse(WebResponse response, Stream stream)
        {
            HttpReadState.WebResponse = response;
            HttpReadState.Stream = stream;
            HttpReadState.Buffer = BufferPool.Get();
        }

        public virtual bool OnRead(int bytesRead)
        {
            var buffer = HttpReadState.Buffer;
            HttpReadState.StringDecoder.Decode(new ArraySegment<byte>(buffer.Array, buffer.Offset, bytesRead));
            
            return true;
        }
                
        public void EndReading()
        {
            this.Value = HttpReadState.StringDecoder.ToString();

            Dispose();
        }

        public void NotifyComplete()
        {
            OnComplete(this.ResponseStatus, this.Value, this.State);
        }

        //Explicit interface as we don't want outsiders calling this guy.
        internal void Dispose()
        {
            BufferPool.Return(HttpReadState.Buffer);

            if (HttpReadState.StringDecoder != null)
            {
                HttpReadState.StringDecoder.Dispose();
            }

            if (HttpReadState.WebRequest != null)
            {
                HttpReadState.WebRequest.Abort();
            }

            if (HttpReadState.WebResponse != null)
            {
                var responseAsDisposable = (IDisposable)HttpReadState.WebResponse;
                responseAsDisposable.Dispose();
            }
            
            //Clears all state and will cause reading for this command to stop.
            HttpReadState = new HttpReadState();
        }
    }
}
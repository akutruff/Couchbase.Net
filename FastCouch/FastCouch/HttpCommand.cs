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
        
        public void BeginRequest(HttpWebRequest request)
        {
            HttpReadState = new HttpReadState(request);
        }
                                                                                     
        public void SetHost(string hostName, int port)
        {
            UriBuilder.Host = hostName;
            UriBuilder.Port = port;
        }
                                                                                     
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

        public void NotifyComplete(ResponseStatus responseStatus)
        {
            this.ResponseStatus = responseStatus;
            NotifyComplete();
        }

        public void OnGotResponse(Stream stream)
        {
            HttpReadState.Stream = stream;
            HttpReadState.Buffer = BufferPool.Get();
        }

        public virtual void OnRead(int bytesRead)
        {
            var buffer = HttpReadState.Buffer;
            HttpReadState.StringDecoder.Decode(new ArraySegment<byte>(buffer.Array, buffer.Offset, bytesRead));
        }

        public void EndReading()
        {
            this.Value = HttpReadState.StringDecoder.ToString();

            if (HttpReadState.StringDecoder != null)
            {
                HttpReadState.StringDecoder.Dispose();
            }

            BufferPool.Return(HttpReadState.Buffer);

            //Clears all state and will cause reading for this command to stop.
            HttpReadState = new HttpReadState();
        }

        public void NotifyComplete()
        {
            OnComplete(this.ResponseStatus, this.Value, this.State);
        }
    }

    public class LineReadingHttpCommand : HttpCommand
    {
        Action<string, object> _onRead;

        public LineReadingHttpCommand(UriBuilder builder, object state, Action<string, object> onRead, Action<ResponseStatus, string, object> onComplete)
            : base(builder, state, onComplete)
        {
            _onRead = onRead;
        }
                                       
        public override void OnRead(int bytesRead)
        {
            var buffer = HttpReadState.Buffer;
            var dataRead = new ArraySegment<byte>(buffer.Array, buffer.Offset, bytesRead);

            string value;
            while (HttpReadState.StringDecoder.DecodeUntilUtf8Character(dataRead, '\n', out value, out dataRead))
            {
                _onRead(value, this.State);
            }
        }
    }
}
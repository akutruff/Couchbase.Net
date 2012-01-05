using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FastCouch
{
    public abstract class MemcachedCommand
    {
        public MemcachedHeader RequestHeader;
                                             

        public int Id { get; private set; }

        public object State { get; private set; }

        public string Key { get; private set; }

        public ResponseStatus ResponseStatus { get; set; }
        
        public string ErrorMessage { get; set; }

        public abstract Opcode Opcode { get; }
        
        public long Cas { get; set; }
        
        public Action<ResponseStatus, string, long, object> OnComplete { get; private set; }

        public int VBucketId
        {
            get { return RequestHeader.VBucket; }
            set { RequestHeader.VBucket = value; }
        }

        public MemcachedCommand(int id, object state, string key, Action<ResponseStatus, string, long, object> onComplete)
        {
            Id = id;
            
            RequestHeader.Opaque = id;
            RequestHeader.Key = key;
            RequestHeader.KeyLength = string.IsNullOrEmpty(key) ? 0 : Encoding.UTF8.GetByteCount(key);
            RequestHeader.TotalBodyLength = RequestHeader.KeyLength;
           
            State = state;
            Key = key;

            OnComplete = onComplete;
            this.ErrorMessage = string.Empty;
        }

        public virtual void Parse(
            ResponseStatus responseStatus,
            ArraySegment<byte> bodyData,
            ArraySegment<byte> extras,
            ArraySegment<byte> key,
            int bytesOfBodyPreviouslyRead,
            int totalBodyLength)
        { 
        }

        public void SetVBucketId(int vBucketId)
        {
            this.VBucketId = vBucketId;
            this.RequestHeader.VBucket = VBucketId;
        }

        public virtual void BeginWriting()
        {
             
        }

        public virtual int WriteValue(ArraySegment<byte> ValueBuffer, int currentByteInValue)
        {
            return 0;    
        }

        public virtual void WriteExtras(ArraySegment<byte> arraySegment)
        {
            int lastIndex = arraySegment.Offset + arraySegment.Count;
            for (int i = arraySegment.Offset; i < lastIndex; i++)
            {
                arraySegment.Array[i] = 0;
            }
        }

        public virtual void WriteKey(ArraySegment<byte> buffer)
        {
            if (RequestHeader.KeyLength == 0)
                return;
            
            Encoding.UTF8.GetBytes(this.Key, 0, this.Key.Length, buffer.Array, buffer.Offset);
        }

        public virtual void NotifyComplete()
        {
            OnComplete(ResponseStatus, string.Empty, this.Cas, this.State);
        }

        public void NotifyComplete(ResponseStatus status)
        {
            ResponseStatus = status;
            NotifyComplete();
        }
    }
}
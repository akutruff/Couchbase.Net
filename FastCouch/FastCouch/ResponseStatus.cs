using System;
using System.Collections.Generic;
using System.Linq;

namespace FastCouch
{
    public enum ResponseStatus 
    {
        NoError = 0x0000,
        KeyNotFound = 0x0001,
        KeyExists = 0x0002,
        ValueTooLarge = 0x0003,
        InvalidArguments = 0x0004,
        ItemNotStored = 0x0005,
        IncrementOrDecrementOnNonNumericValue = 0x0006,
        VbucketBelongsToAnotherServer = 0x0007,
        AuthenticationError = 0x0008,
        AuthenticationContinue = 0x0009,
        UnknownCommand = 0x0081,
        OutOfMemory = 0x0082,
        NotSupported = 0x0083,
        InternalError = 0x0084,
        Busy = 0x0085,
        TemporaryFailure = 0x0086,

        //These are not official memcache response statuses.  
        DisconnectionOccuredBeforeOperationCouldBeSent = 0x8000,
        DisconnectionOccuredWhileOperationWaitingToBeSent = 0x8001,
    }
}

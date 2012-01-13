using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FastCouch
{
    public static class AsyncPattern
    {
        public static AsyncPattern<TTarget> Create<TTarget>(
            Func<TTarget, AsyncPattern<TTarget>, IAsyncResult> beginAsyncer,
            Func<IAsyncResult, TTarget> handlerThatCallsEndAsync,
            Func<IAsyncResult, Exception, TTarget> errorHandler)
            where TTarget : class
        {
            return new AsyncPattern<TTarget>(beginAsyncer, handlerThatCallsEndAsync, errorHandler);
        }

        public static AsyncPattern<TTarget, TState> Create<TTarget, TState>(
            Func<TTarget, AsyncPattern<TTarget, TState>, TState, IAsyncResult> beginAsyncer,
            Func<IAsyncResult, TState, AsyncPatternResult<TTarget, TState>> handlerThatCallsEndAsync,
            Func<IAsyncResult, TState, Exception, AsyncPatternResult<TTarget, TState>> errorHandler)
            where TTarget : class
            where TState : class
        {
            return new AsyncPattern<TTarget, TState>(beginAsyncer, handlerThatCallsEndAsync, errorHandler);
        }
    }

    public class AsyncPattern<TTarget>
        where TTarget : class
    {
        private Func<TTarget, AsyncPattern<TTarget>, IAsyncResult> _beginAsyncer;
        private Func<IAsyncResult, TTarget> _handlerThatCallsEndAsync;
        private Func<IAsyncResult, Exception, TTarget> _errorHandler;

        public AsyncPattern(
            Func<TTarget, AsyncPattern<TTarget>, IAsyncResult> beginAsyncer, 
            Func<IAsyncResult, TTarget> handlerThatCallsEndAsync, 
            Func<IAsyncResult, Exception, TTarget> errorHandler)
        {
            _beginAsyncer = beginAsyncer;
            _handlerThatCallsEndAsync = handlerThatCallsEndAsync;
            _errorHandler = errorHandler;
        }

        public TTarget Stop()
        {
            return null;
        }

        public TTarget Continue(TTarget target)
        {
            return target;
        }

        public bool BeginAsync(TTarget target)
        {
            while (true)
            {
                IAsyncResult result = null;

                TTarget nextTarget;
                try
                {
                    result = _beginAsyncer(target, this);

                    if (!result.CompletedSynchronously)
                        return true; ;

                    nextTarget = _handlerThatCallsEndAsync(result);

                    if (nextTarget == null)
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine(e.ToString());
                    nextTarget = _errorHandler(result, e);

                    if (nextTarget == null)
                    {
                        return false;
                    }
                }

                target = nextTarget;
            }
        }

        public void OnCompleted(IAsyncResult result)
        {
            if (result.CompletedSynchronously)
                return;

            TTarget nextTarget;

            try
            {
                nextTarget = _handlerThatCallsEndAsync(result);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine(e.ToString());
                nextTarget = _errorHandler(result, e);
            }

            if (nextTarget== null)
                return;

            BeginAsync(nextTarget);
        }
    }


    public struct AsyncPatternResult<TTarget, TState>
    {
        public readonly TTarget Target; 
        public readonly TState State;

        internal AsyncPatternResult(TTarget target, TState state)
        {
            Target = target;
            State = state;
        }
    }

    public class AsyncPattern<TTarget, TState>
        where TTarget : class
        where TState : class
    {

        private Func<TTarget, AsyncPattern<TTarget, TState>, TState, IAsyncResult> _beginAsyncer;
        private Func<IAsyncResult, TState, AsyncPatternResult<TTarget, TState>> _handlerThatCallsEndAsync;
        private Func<IAsyncResult, TState, Exception, AsyncPatternResult<TTarget, TState>> _errorHandler;

        public AsyncPattern(
            Func<TTarget, AsyncPattern<TTarget, TState>, TState, IAsyncResult> beginAsyncer, 
            Func<IAsyncResult, TState, AsyncPatternResult<TTarget, TState>> handlerThatCallsEndAsync, 
            Func<IAsyncResult, TState, Exception, AsyncPatternResult<TTarget, TState>> errorHandler)
        {
            _beginAsyncer = beginAsyncer;
            _handlerThatCallsEndAsync = handlerThatCallsEndAsync;
            _errorHandler = errorHandler;
        }

        public AsyncPatternResult<TTarget, TState> Stop()
        {
            return new AsyncPatternResult<TTarget, TState>();
        }

        public AsyncPatternResult<TTarget, TState> Continue(TTarget target, TState state)
        {
            return new AsyncPatternResult<TTarget, TState>(target, state);
        }

        public bool BeginAsync(
            TTarget target,
            TState state)
        {
            while (true)
            {
                IAsyncResult result = null;

                AsyncPatternResult<TTarget, TState> nextTargetAndState;
                try
                {
                    result = _beginAsyncer(target, this, state);

                    if (!result.CompletedSynchronously)
                        return true; ;

                    nextTargetAndState = _handlerThatCallsEndAsync(result, (TState)result.AsyncState);

                    if (nextTargetAndState.Target == null)
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    Console.WriteLine(e.ToString());

                    nextTargetAndState = _errorHandler(result, (TState)result.AsyncState, e);
                    
                    if (nextTargetAndState.Target == null)
                    {
                        return false;
                    }
                }

                target = nextTargetAndState.Target;
                state = nextTargetAndState.State;
            }
        }

        public void OnCompleted(IAsyncResult result)
        {
            if (result.CompletedSynchronously)
                return;
            
            Thread.MemoryBarrier();

            AsyncPatternResult<TTarget, TState> nextTargetAndState;

            try
            {
                nextTargetAndState = _handlerThatCallsEndAsync(result, (TState)result.AsyncState);
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine(e.ToString());
                nextTargetAndState = _errorHandler(result, (TState)result.AsyncState, e);
            }

            if (nextTargetAndState.Target == null)
                return;

            BeginAsync(nextTargetAndState.Target, nextTargetAndState.State);
        }
    }
}

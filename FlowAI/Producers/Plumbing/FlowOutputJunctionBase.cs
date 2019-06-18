using System;
using System.Collections.Async;
using System.Collections.Concurrent;


namespace FlowAI
{
    /// <summary>
    /// Abstract class for an output junction, which can't offer an equivalent base implementation to a FlowInputJunction.
    /// </summary>
    public abstract class FlowOutputJunctionBase<T> : FlowProducerBase<T>
    {
        public IProducerConsumerCollection<Func<IAsyncEnumerator<T>>> Flows { get; }
        public FlowOutputJunctionBase(params Func<IAsyncEnumerator<T>>[] flows)
        {
            Flows = new ConcurrentQueue<Func<IAsyncEnumerator<T>>>(flows);
        }
    }
}

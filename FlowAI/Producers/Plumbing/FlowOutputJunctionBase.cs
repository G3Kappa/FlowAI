using System.Collections.Concurrent;


namespace FlowAI
{
    /// <summary>
    /// Abstract class for an output junction, which can't offer an equivalent base implementation to a FlowInputJunction.
    /// </summary>
    public abstract class FlowOutputJunctionBase<T> : FlowProducerBase<T>
    {
        public IProducerConsumerCollection<IFlowProducer<T>> Producers { get; }
        public FlowOutputJunctionBase(params IFlowProducer<T>[] producers)
        {
            Producers = new ConcurrentBag<IFlowProducer<T>>(producers);
        }
    }
}

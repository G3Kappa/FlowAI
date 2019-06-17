using System.Collections.Async;
using System.Collections.Concurrent;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Copies and redirects droplets from any flow that passes throguh the junction to a number of consumers in parallel.
    /// </summary>
    public class FlowInputJunction<T> : IFlowConsumer<T>
    {
        public IProducerConsumerCollection<IFlowConsumer<T>> Consumers { get; }
        public FlowInputJunction(params IFlowConsumer<T>[] consumers)
        {
            Consumers = new ConcurrentBag<IFlowConsumer<T>>(consumers);
        }

        public virtual async Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet)
        {
            bool allFalse = true;
            foreach (IFlowConsumer<T> c in Consumers)
            {
                if(await c.ConsumeDroplet(producer, droplet))
                {
                    allFalse = false;
                }
            }
            return !allFalse;
        }
        public IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    bool stored = await ConsumeDroplet(producer, t);
                    await yield.ReturnAsync(stored);
                });
            });
        }
    }
}

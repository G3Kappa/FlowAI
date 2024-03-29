﻿using FlowAI.Producers;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Threading.Tasks;


namespace FlowAI.Consumers.Plumbing
{
    /// <summary>
    /// Copies and redirects droplets from any flow that passes throguh the junction to a number of consumers in parallel.
    /// </summary>
    public class FlowInputJunction<T> : IFlowConsumer<T>
    {
        public IProducerConsumerCollection<IFlowConsumer<T>> Consumers { get; }
        public FlowInputJunction(params IFlowConsumer<T>[] consumers)
        {
            Consumers = new ConcurrentQueue<IFlowConsumer<T>>(consumers);
        }

        public virtual async Task<bool> ConsumeDroplet(T droplet)
        {
            bool allFalse = true;
            foreach (IFlowConsumer<T> c in Consumers)
            {
                if(await c.ConsumeDroplet(droplet))
                {
                    allFalse = false;
                }
            }
            return !allFalse;
        }
        public IAsyncEnumerator<bool> ConsumeFlow(IAsyncEnumerator<T> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    bool stored = await ConsumeDroplet(t);
                    await yield.ReturnAsync(stored);
                });
            });
        }
    }
}

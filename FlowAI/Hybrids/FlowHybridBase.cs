﻿using System;
using System.Collections.Async;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Base class for a flow component that produces and consumes droplets at the same time. 
    /// </summary>
    public abstract class FlowHybridBase<T> : FlowProducerBase<T>, IFlowConsumer<T>
    {
        public abstract Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet);
        public abstract IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow);

        /// <summary>
        /// Pipes an input flow into this component, then returns the output flow.
        /// </summary>
        public virtual IAsyncEnumerator<T> PipeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow, Predicate<T> stop = null, int maxDroplets = 0)
        {
            // The default implementation is 1-1 dripping, while FlowMachines have a tailored and more efficient nInputs:nOutputs flowing implementation.
            return new AsyncEnumerator<T>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    await ConsumeDroplet(producer, t);
                    if (IsFlowStarted()) // Don't deadlock if e.g. the output buffer is empty
                    {
                        await yield.ReturnAsync(await Drip());
                    }
                });
            });
        }
    }
}
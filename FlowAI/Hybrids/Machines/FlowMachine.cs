﻿using System;
using System.Collections.Async;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// A consumer-producer that manages an input and an output buffer, consuming from the input and producing from the output.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FlowMachine<T> : FlowHybridBase<T>
    {
        protected CyclicFlowBuffer<T> InputBuffer { get; }
        protected CyclicFlowBuffer<T> OutputBuffer { get; }

        public FlowMachine(int nInputs, int nOutputs) : base()
        {
            InputBuffer = new CyclicFlowBuffer<T>(nInputs);
            OutputBuffer = new CyclicFlowBuffer<T>(nOutputs);
        }

        /// <summary>
        /// Called whenever a droplet is pulled from the output buffer.
        /// </summary>
        /// <param name="inBuf">The input buffer</param>
        /// <param name="outBuf">The output buffer</param>
        public abstract Task Update(FlowBuffer<T> inBuf, FlowBuffer<T> outBuf);

        public override async Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet)
        {
            var ret = await InputBuffer.ConsumeDroplet(producer, droplet);
            await Update(InputBuffer, OutputBuffer);
            return ret;
        }
        public override IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow) => InputBuffer.ConsumeFlow(producer, flow);
        public sealed override async Task<T> Drip()
        {
            return await OutputBuffer.Drip();
        }
        public override IAsyncEnumerator<T> PipeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow, Predicate<T> stop = null, int maxDroplets = 0)
        {
            // The default implementation is 1-1 dripping, while FlowMachines have a tailored and more efficient nInputs:nOutputs flowing implementation.

            // ... Unless their input buffer has unlimited size, then they're 1-1
            if(InputBuffer.Capacity == 0)
            {
                return base.PipeFlow(producer, flow, stop, maxDroplets);
            }

            return new AsyncEnumerator<T>(async yield =>
            {
                bool hasNext = await flow.MoveNextAsync();
                while(hasNext)
                {
                    await ConsumeDroplet(producer, flow.Current);
                    hasNext = await flow.MoveNextAsync();

                    if(OutputBuffer.Contents.Count > 0)
                    {
                        await Flow(stop: t => OutputBuffer.Contents.Count == 0 || (stop?.Invoke(t) ?? false), maxDroplets: OutputBuffer.Capacity).ForEachAsync(async t =>
                        {
                            await yield.ReturnAsync(t);
                        });
                    }
                }
            });
        }
    }
}

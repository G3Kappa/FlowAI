using FlowAI.Hybrids.Buffers;
using FlowAI.Producers;
using System;
using System.Collections.Async;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
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
        /// Called whenever a droplet is pushed into the input buffer.
        /// Pushing into the output buffer will signal the start of a chunk of data.
        /// </summary>
        /// <param name="inBuf">The input buffer</param>
        /// <param name="outBuf">The output buffer</param>
        public abstract Task Update(FlowBuffer<T> inBuf, FlowBuffer<T> outBuf);

        public override async Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet)
        {
            bool ret = await InputBuffer.ConsumeDroplet(producer, droplet);
            await Update(InputBuffer, OutputBuffer);
            return ret;
        }
        public override IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow)
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

        public sealed override async Task<T> Drip()
        {
            return await OutputBuffer.Drip();
        }

        public override IAsyncEnumerator<T> PipeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow, Predicate<T> stop = null, int maxDroplets = 0)
        {
            // The default implementation is 1-1 dripping, while FlowMachines have a tailored and more efficient nInputs:nOutputs flowing implementation.

            // ... Unless their input buffer has unlimited size, then they're 1-1

            return InputBuffer.Capacity == 0
                ? base.PipeFlow(producer, flow, stop, maxDroplets)
                : new AsyncEnumerator<T>(async yield =>
                {
                    bool hasNext = await flow.MoveNextAsync();
                    while(hasNext)
                    {
                        await ConsumeDroplet(producer, flow.Current);
                        hasNext = await flow.MoveNextAsync();

                        if(OutputBuffer.Contents.Count > 0)
                        {
                            await Flow(stop: t => OutputBuffer.Empty || (stop?.Invoke(t) ?? false), maxDroplets: OutputBuffer.Capacity).ForEachAsync(async t =>
                            {
                                await yield.ReturnAsync(t);
                            });
                        }
                    }
                });
        }
    }
}

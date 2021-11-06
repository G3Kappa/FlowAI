using FlowAI.Exceptions;
using FlowAI.Hybrids.Buffers;
using FlowAI.Producers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
{
    /// <summary>
    /// A consumer-producer that manages an input and an output buffer, consuming from the input and producing from the output.
    /// </summary>
    public abstract class FlowMachineBase<TInput, TOutput> : FlowHybridBase<TInput, TOutput>
    {
        protected CyclicFlowBuffer<TInput> InputBuffer { get; }
        protected CyclicFlowBuffer<TOutput> OutputBuffer { get; }

        protected bool ShouldUpdate { get; set; } = true;

        public FlowMachineBase(int nInputs, int nOutputs) : base()
        {
            InputBuffer = new CyclicFlowBuffer<TInput>(nInputs);
            OutputBuffer = new CyclicFlowBuffer<TOutput>(nOutputs);
        }

        /// <summary>
        /// Called whenever a droplet is pushed into the input buffer.
        /// Pushing into the output buffer will signal the start of a chunk of data.
        /// </summary>
        /// <param name="inBuf">The input buffer</param>
        /// <param name="outBuf">The output buffer</param>
        public abstract Task Update(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf);
        /// <summary>
        /// Called when the input flow is staunched and the machine still has contents in its input buffer.
        /// </summary>
        /// <param name="inBuf">The input buffer</param>
        /// <param name="outBuf">The output buffer</param>
        public abstract Task Flush(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf);

        public override async Task<bool> ConsumeDroplet(TInput droplet)
        {
            bool ret = await InputBuffer.ConsumeDroplet(droplet);
            if(ShouldUpdate)
            {
                await Update(InputBuffer, OutputBuffer);
            }
            return ret;
        }
        public override async IAsyncEnumerable<bool> ConsumeFlow(IAsyncEnumerable<TInput> flow)
        {
            await foreach(var t in flow) {
                yield return await ConsumeDroplet(t);
            }
        }

        public sealed override async Task<TOutput> Drip()
        {
            return await OutputBuffer.Drip();
        }

        public override IAsyncEnumerable<TOutput> PipeFlow(IAsyncEnumerable<TInput> flow, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            // The default implementation is 1-1 dripping, while FlowMachines have a tailored and more efficient nInputs:nOutputs flowing implementation.

            // ... Unless their input buffer has unlimited size, then they're 1-1

            return InputBuffer.Capacity == 0
                ? base.PipeFlow(flow, stop, maxDroplets)
                : Inner();

            async IAsyncEnumerable<TOutput> Inner()
            {
                var enumerator = flow.GetAsyncEnumerator();
                var hasNext = await enumerator.MoveNextAsync();
                while (hasNext) {
                    await ConsumeDroplet(enumerator.Current);
                    hasNext = await enumerator.MoveNextAsync();

                    if (!hasNext && !InputBuffer.Empty) {
                        await Flush(InputBuffer, OutputBuffer);
                    }

                    if (!OutputBuffer.Empty) {
                        await foreach(var t in Flow(stop: t => --maxDroplets == 0 || OutputBuffer.Empty || (stop?.Invoke(t) ?? false), maxDroplets: OutputBuffer.Capacity)) {
                            yield return t;
                        }
                    }
                }
            }
        }
    }
}

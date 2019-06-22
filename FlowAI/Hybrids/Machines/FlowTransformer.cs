using FlowAI.Hybrids.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
{
    /// <summary>
    /// A machine that transforms and changes the type of chunks of droplets as it consumes them. Like a generalized FlowMapper, but it requires a few extra parameters in order to work.
    /// </summary>
    public class FlowTransformer<TInput, TOutput> : FlowMachineBase<TInput, TOutput>
    {
        public Func<TInput[], TOutput[]> Map { get; }
        public Func<TInput[], TOutput[], bool> ConsumeIf { get; }
        public int ChunkSize { get; protected set; }

        public FlowTransformer(Func<TInput[], TOutput[]> mapping, Func<TInput[], TOutput[], bool> consumeIf, int chunkSize) : base(chunkSize, 0)
        {
            Map = mapping;
            ChunkSize = chunkSize;
            ConsumeIf = consumeIf;
        }

        public override async Task Update(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf)
        {
            TInput[] oldContents = inBuf.Contents.ToArray();
            TOutput[] mapped = Map(oldContents);
            if (ConsumeIf(oldContents, mapped))
            {
                await inBuf.Flow(maxDroplets: inBuf.Capacity).Collect();
                mapped = OnInputTransformed(oldContents, mapped);
                await outBuf.ConsumeFlow(this, mapped.GetAsyncEnumerator()).Collect();
            }
            else if (inBuf.Full)
            {
                await inBuf.Drip();
                await outBuf.ConsumeDroplet(this, mapped[0]);
            }
        }

        /// <summary>
        /// Called whenever a mapping operation takes place, right before the output buffer loads it.
        /// </summary>
        protected virtual TOutput[] OnInputTransformed(TInput[] input, TOutput[] output)
        {
            return output;
        }
    }
}

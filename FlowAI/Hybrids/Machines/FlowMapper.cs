using FlowAI.Hybrids.Buffers;
using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
{
    /// <summary>
    /// A machine that transforms chunks of droplets as it consumes them.
    /// More powerful than a DropletFlowMapper, which only works on 1-1 streams.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FlowMapper<T> : FlowMachine<T>
    {
        public Func<T[], T[]> Map { get; }
        public int ChunkSize { get; protected set; }

        public FlowMapper(Func<T[], T[]> mapping, int chunkSize) : base(chunkSize, 0)
        {
            Map = mapping;
            ChunkSize = chunkSize;
        }

        public override async Task Update(FlowBuffer<T> inBuf, FlowBuffer<T> outBuf)
        {
            T[] oldContents = inBuf.Contents.ToArray();
            T[] mapped = Map(oldContents);
            if (!mapped.SequenceEqual(oldContents))
            {
                await inBuf.Flow(maxDroplets: inBuf.Capacity).Collect();
                mapped = OnInputTransformed(oldContents, mapped);
                await outBuf.ConsumeFlow(this, mapped.GetAsyncEnumerator()).Collect();
            }
            else if (inBuf.Full)
            {
                await outBuf.ConsumeDroplet(inBuf, await inBuf.Drip());
            }
        }

        /// <summary>
        /// Called whenever a mapping operation takes place, right before the output buffer loads it.
        /// </summary>
        protected virtual T[] OnInputTransformed(T[] input, T[] output)
        {
            return output;
        }
    }
}

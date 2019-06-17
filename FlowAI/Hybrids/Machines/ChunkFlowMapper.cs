using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// A machine that transforms chunks of droplets as it consumes them
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ChunkFlowMapper<T> : FlowMachine<T>
    {
        public Func<T[], T[]> Map { get; }
        public int ChunkSize { get; protected set; }

        public ChunkFlowMapper(Func<T[], T[]> mapping, int chunkSize) : base(chunkSize, 0)
        {
            Map = mapping;
            ChunkSize = chunkSize;
        }

        public override async Task Update(FlowBuffer<T> inBuf, FlowBuffer<T> outBuf)
        {
            var oldContents = inBuf.Contents.ToList();
            var mapped = Map(oldContents.ToArray());
            if(!mapped.SequenceEqual(oldContents) || inBuf.Contents.Count == inBuf.Capacity)
            {
                await inBuf.Flow(maxDroplets: inBuf.Capacity).Collect();
                await outBuf.ConsumeFlow(this, mapped.GetAsyncEnumerator()).Collect();
            }
        }
    }
}

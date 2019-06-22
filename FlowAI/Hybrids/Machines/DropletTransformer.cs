using FlowAI.Hybrids.Buffers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
{
    /// <summary>
    /// A simpler version of the FlowTransformer that only works one droplet at a time.
    /// </summary>
    public class DropletTransformer<TInput, TOutput> : FlowMachineBase<TInput, TOutput>
    {
        public Func<TInput, TOutput> Map { get; }

        public DropletTransformer(Func<TInput, TOutput> mapping) : base(0, 0)
        {
            Map = mapping;
        }

        public override async Task Flush(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf)
        {
             await outBuf.ConsumeDroplet(this, Map(await inBuf.Drip()));
        }

        public override async Task Update(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf)
        {
            await outBuf.ConsumeDroplet(this, Map(await inBuf.Drip()));
        }
    }
}

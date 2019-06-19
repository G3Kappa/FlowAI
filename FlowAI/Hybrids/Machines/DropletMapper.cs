using FlowAI.Hybrids.Buffers;
using System;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
{
    /// <summary>
    /// A simple mapper that transforms droplets as it consumes them.
    /// For anything more complex, consider using a FlowMapper.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DropletMapper<T> : FlowMachineBase<T>
    {
        public Func<T, T> Map { get; }

        public DropletMapper(Func<T, T> mapping) : base(0, 0)
        {
            Map = mapping;
        }

        public override async Task Update(FlowBuffer<T> inBuf, FlowBuffer<T> outBuf)
        {
            await outBuf.ConsumeDroplet(this, Map(await inBuf.Drip()));
        }
    }
}

using System;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// A machine that transforms droplets as it consumes them
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DropletFlowMapper<T> : FlowMachine<T>
    {
        public Func<T, T> Map { get; }

        public DropletFlowMapper(Func<T, T> mapping) : base(0, 0)
        {
            Map = mapping;
        }

        public override async Task Update(FlowBuffer<T> inBuf, FlowBuffer<T> outBuf)
        {
            await outBuf.ConsumeDroplet(this, Map(await inBuf.Drip()));
        }
    }
}

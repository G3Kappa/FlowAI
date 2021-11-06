using FlowAI.Producers;
using System;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Buffers
{
    public class MinDropletBuffer<T> : DropletBuffer<T>
    {
        public Comparison<T> Comparer { get; }

        public MinDropletBuffer(Comparison<T> comparer)
        {
            Comparer = comparer;
        }

        public override Task<bool> ConsumeDroplet(T droplet)
        {
            if (Empty || Comparer(Contents.ElementAt(0), droplet) > 0)
            {
                return base.ConsumeDroplet(droplet);
            }
            return Task.FromResult(true); // This buffer can never be full
        }
    }
}

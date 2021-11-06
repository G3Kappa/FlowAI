using FlowAI.Producers;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Buffers
{
    /// <summary>
    /// Like a regular buffer, but instead of getting full it will delete the oldest element, 
    /// meaning that technically this buffer can never be full unless async shenanigans happen.
    /// </summary>
    public class CyclicFlowBuffer<T> : FlowBuffer<T>
    {
        public CyclicFlowBuffer(int capacity) : base(capacity) { }

        public override Task<bool> ConsumeDroplet(T droplet)
        {
            if (Capacity > 0 && Queue.Count >= Capacity)
            {
                if (Queue.TryDequeue(out _))
                {
                    Queue.Enqueue(droplet);
                    return Task.FromResult(true);
                }
                return Task.FromResult(false);
            }
            else
            {
                Queue.Enqueue(droplet);
                return Task.FromResult(true);
            }
        }
    }
}

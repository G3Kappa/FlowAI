using System.Collections.Async;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Merges the flow of a number of producers into a single Flow by alternating chunks of droplets from each producer.
    /// </summary>
    public class SplittingFlowOutputJunction<T> : FlowOutputJunctionBase<T>
    {
        public int ChunkSize { get; protected set; } = 1;
        public int CurrentDroplet { get; protected set; } = 0;
        public int Current { get; protected set; } = 0;
        public override bool IsFlowStarted() => base.IsFlowStarted() && Producers != null && Producers.Count > 0;
        public SplittingFlowOutputJunction(int chunkSize = 1, params IFlowProducer<T>[] producers) : base(producers)
        {
            ChunkSize = chunkSize <= 0 ? 1 : chunkSize;
        }

        public override async Task<T> Drip()
        {
            if (Current >= Producers.Count)
            {
                Current = 0;
            }
            T ret = await Producers.ToArray()[Current].Drip();
            if (++CurrentDroplet == ChunkSize)
            {
                CurrentDroplet = 0;
                Current++;
            }
            return ret;
        }
    }
}

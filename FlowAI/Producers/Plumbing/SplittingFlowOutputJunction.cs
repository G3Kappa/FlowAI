using FlowAI.Exceptions;
using System;
using System.Collections.Async;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Producers.Plumbing
{
    /// <summary>
    /// Merges the flow of a number of producers into a single Flow by alternating chunks of droplets from each producer.
    /// </summary>
    public class SplittingFlowOutputJunction<T> : FlowOutputJunctionBase<T, T>
    {
        public int ChunkSize { get; protected set; } = 1;
        public int CurrentDroplet { get; protected set; } = 0;
        public int Current { get; protected set; } = 0;
        public override bool IsFlowStarted => base.IsFlowStarted && FlowStarters != null && FlowStarters.Count > 0;

        public SplittingFlowOutputJunction(int chunkSize = 1, params Func<IAsyncEnumerator<T>>[] flows) : base(flows)
        {
            ChunkSize = chunkSize <= 0 ? 1 : chunkSize;
        }

        public override async Task<T> Drip()
        {
            if (Current >= FlowStarters.Count)
            {
                Current = 0;
            }

            IAsyncEnumerator<T>[] flows = GetFlows();
            if(await flows[Current].MoveNextAsync())
            {
                T ret = flows[Current].Current;
                if (++CurrentDroplet == ChunkSize)
                {
                    CurrentDroplet = 0;
                    Current++;
                }
                return ret;
            }

            await InterruptFlow(new FlowInterruptedException<T>(this, "Drip", fatal: false)); return default;
        }
    }
}

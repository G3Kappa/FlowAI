using FlowAI.Exceptions;
using System;
using System.Collections.Async;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Producers.Plumbing
{
    /// <summary>
    /// Merges the flow of a number of producers into a single Flow by exhausting each flow sequentially in the order they were provided.
    /// </summary>
    public class SequentialFlowOutputJunction<T> : FlowOutputJunctionBase<T>
    {
        public int Current { get; protected set; } = 0;
        public override bool IsFlowStarted => base.IsFlowStarted && FlowStarters != null && FlowStarters.Count > 0;

        public SequentialFlowOutputJunction(params Func<IAsyncEnumerator<T>>[] flows) : base(flows)
        {
        }

        public override async Task<T> Drip()
        {
            if (Current >= FlowStarters.Count)
            {
                Current = 0;
            }

            bool flowExhausted;
            IAsyncEnumerator<T>[] flows = GetFlows();
            while ((flowExhausted = !await flows[Current].MoveNextAsync()) && ++Current < FlowStarters.Count) ;

            if(!flowExhausted)
            {
                T ret = flows[Current].Current;
                return ret;
            }

            await InterruptFlow(); return default;
        }
    }
}

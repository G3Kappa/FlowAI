using FlowAI.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Producers.Plumbing
{
    /// <summary>
    /// Merges the flow of a number of producers into a single Flow by encapsulating all parallel droplets into an array.
    /// </summary>
    public class MergingFlowOutputJunction<T> : FlowOutputJunctionBase<T, T[]>
    {
        public override bool IsFlowStarted => base.IsFlowStarted && FlowStarters != null && FlowStarters.Count > 0;
        public MergingFlowOutputJunction(params Func<IAsyncEnumerable<T>>[] flows) : base(flows)
        {
        }

        public override async Task<T[]> Drip()
        {
            var flows = GetFlows();
            if(await flows[0].MoveNextAsync())
            {
                var rets = new List<T>()
                {
                    flows[0].Current
                };
                for (int i = 1; i < FlowStarters.Count; i++)
                {
                    if (await flows[i].MoveNextAsync())
                    {
                        rets.Add(flows[i].Current);
                    }
                }
                return rets.ToArray();
            }

            await InterruptFlow(new FlowInterruptedException<T[]>(this, "Drip", fatal: false)); return default;
        }
    }
}

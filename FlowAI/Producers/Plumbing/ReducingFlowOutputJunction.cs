using FlowAI.Exceptions;
using System;
using System.Collections.Async;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Merges the flow of a number of producers into a single Flow by applying a reduction function over one droplet from all producers at a time.
    /// </summary>
    public class ReducingFlowOutputJunction<T> : FlowOutputJunctionBase<T>
    {
        public Func<T, T, T> Reduce { get; set; }

        public override bool IsFlowStarted() => base.IsFlowStarted() && FlowStarters != null && FlowStarters.Count > 0;
        public ReducingFlowOutputJunction(Func<T, T, T> reduce, params Func<IAsyncEnumerator<T>>[] flows) : base(flows)
        {
            Reduce = reduce;
        }

        public override async Task<T> Drip()
        {
            IAsyncEnumerator<T>[] flows = GetFlows();
            /*
                TODO: This is skipping values in the latest test.
                It might have to do with flows now being accessible by function call.
             */
            if(await flows[0].MoveNextAsync())
            {
                T ret = flows[0].Current;
                for (int i = 1; i < FlowStarters.Count; i++)
                {
                    if (await flows[i].MoveNextAsync())
                    {
                        ret = Reduce(ret, flows[i].Current);
                    }
                }
                return ret;
            }

            await InterruptFlow(new FlowInterruptedException<T>(this, "Drip", fatal: false)); return default;
        }
    }
}

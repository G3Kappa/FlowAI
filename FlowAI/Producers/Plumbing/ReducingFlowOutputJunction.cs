using FlowAI.Exceptions;
using System;
using System.Collections.Async;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Merges the flow of a number of producers into a single Flow by applying a reduction function over one droplet from all producers at a time.
    /// </summary>
    public class ReducingFlowOutputJunction<T> : FlowOutputJunctionBase<T>
    {
        public Func<T, T, T> Reduce { get; set; }

        public override bool IsFlowStarted() => base.IsFlowStarted() && Flows != null && Flows.Count > 0;
        public ReducingFlowOutputJunction(Func<T, T, T> reduce, params IAsyncEnumerator<T>[] flows) : base(flows)
        {
            Reduce = reduce;
        }

        public override async Task<T> Drip()
        {
            IAsyncEnumerator<T>[] flows = Flows.ToArray();
            if(await flows[0].MoveNextAsync())
            {
                T ret = flows[0].Current;
                for (int i = 1; i < Flows.Count; i++)
                {
                    if (!await flows[i].MoveNextAsync())
                    {
                        await InterruptFlow(new FlowInterruptedException<T>(this, "Drip", fatal: false));
                    }
                    ret = Reduce(ret, flows[i].Current);
                }
                return ret;
            }

            await InterruptFlow(new FlowInterruptedException<T>(this, "Drip", fatal: false)); return default;
        }
    }
}

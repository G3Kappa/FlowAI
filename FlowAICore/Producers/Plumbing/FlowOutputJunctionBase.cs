using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlowAI.Producers.Plumbing
{
    /// <summary>
    /// Abstract class for an output junction, which can't offer an equivalent base implementation to a FlowInputJunction.
    /// </summary>
    public abstract class FlowOutputJunctionBase<TInput, TOutput> : FlowProducerBase<TOutput>
    {
        public IProducerConsumerCollection<Func<IAsyncEnumerable<TInput>>> FlowStarters { get; }
        public IProducerConsumerCollection<CappedAsyncEnumerator<TInput>> OpenFlows { get; private set; }

        public FlowOutputJunctionBase(params Func<IAsyncEnumerable<TInput>>[] flows)
        {
            FlowStarters = new ConcurrentQueue<Func<IAsyncEnumerable<TInput>>>(flows);
            OpenFlows = new ConcurrentQueue<CappedAsyncEnumerator<TInput>>(flows.Select(f => new CappedAsyncEnumerator<TInput>(f().GetAsyncEnumerator())).ToArray());
        }

        public IAsyncEnumerator<TInput>[] GetFlows()
        {
            var ret = new IAsyncEnumerator<TInput>[FlowStarters.Count];
            for(var i = 0; i < ret.Length; ++i) {
                OpenFlows.TryTake(out var flow);
                if (flow.IsEnumerationComplete) {
                    var e = new CappedAsyncEnumerator<TInput>(FlowStarters.ElementAt(i)().GetAsyncEnumerator());
                    OpenFlows.TryAdd(e);
                    ret[i] = e;
                    continue;
                }
                OpenFlows.TryAdd(flow);
                ret[i] = flow;
            }
            return ret;
        }

    }
}

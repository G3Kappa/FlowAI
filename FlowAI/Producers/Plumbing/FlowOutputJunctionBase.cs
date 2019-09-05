using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace FlowAI.Producers.Plumbing
{
    /// <summary>
    /// Abstract class for an output junction, which can't offer an equivalent base implementation to a FlowInputJunction.
    /// </summary>
    public abstract class FlowOutputJunctionBase<TInput, TOutput> : FlowProducerBase<TOutput>
    {
        public IProducerConsumerCollection<Func<IAsyncEnumerator<TInput>>> FlowStarters { get; }
        public IProducerConsumerCollection<IAsyncEnumerator<TInput>> OpenFlows { get; private set; }

        public FlowOutputJunctionBase(params Func<IAsyncEnumerator<TInput>>[] flows)
        {
            FlowStarters = new ConcurrentQueue<Func<IAsyncEnumerator<TInput>>>(flows);
            OpenFlows = new ConcurrentQueue<IAsyncEnumerator<TInput>>(flows.Select(f => f()).ToArray());
        }

        public IAsyncEnumerator<TInput>[] GetFlows()
        {
            int i = 0; var ret = new IAsyncEnumerator<TInput>[FlowStarters.Count];
            foreach (IAsyncEnumerator<TInput> flow in OpenFlows)
            {
                ret[i] = flow is AsyncEnumerator<TInput> inst && inst.IsEnumerationComplete 
                    ? FlowStarters.ElementAt(i)() : OpenFlows.ElementAt(i);
                i++;
            }
            return ret;
        }

    }
}

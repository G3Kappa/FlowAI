using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace FlowAI
{
    /// <summary>
    /// Abstract class for an output junction, which can't offer an equivalent base implementation to a FlowInputJunction.
    /// </summary>
    public abstract class FlowOutputJunctionBase<T> : FlowProducerBase<T>
    {
        public IProducerConsumerCollection<Func<IAsyncEnumerator<T>>> FlowStarters { get; }
        public IProducerConsumerCollection<IAsyncEnumerator<T>> OpenFlows { get; private set; }

        public FlowOutputJunctionBase(params Func<IAsyncEnumerator<T>>[] flows)
        {
            FlowStarters = new ConcurrentQueue<Func<IAsyncEnumerator<T>>>(flows);
            OpenFlows = new ConcurrentQueue<IAsyncEnumerator<T>>(flows.Select(f => f()).ToArray());
        }

        public IAsyncEnumerator<T>[] GetFlows()
        {
            int i = 0; var ret = new IAsyncEnumerator<T>[FlowStarters.Count];
            foreach (IAsyncEnumerator<T> flow in OpenFlows)
            {
                if(flow is AsyncEnumerator<T> inst && inst.IsEnumerationComplete)
                {
                    ret[i] = FlowStarters.ElementAt(i)();
                }
                else
                {
                    ret[i] = OpenFlows.ElementAt(i);
                }
                i++;
            }
            return ret;
        }

    }
}

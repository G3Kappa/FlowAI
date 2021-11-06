using FlowAI.Consumers;
using FlowAI.Producers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace FlowAI.Hybrids
{
    /// <summary>
    /// Base class for a flow component that produces and consumes droplets at the same time. 
    /// </summary>
    public abstract class FlowHybridBase<TInput, TOutput> : FlowProducerBase<TOutput>, IFlowConsumer<TInput>
    {
        public abstract Task<bool> ConsumeDroplet(TInput droplet);
        public abstract IAsyncEnumerable<bool> ConsumeFlow(IAsyncEnumerable<TInput> flow);

        /// <summary>
        /// Pipes an input flow into this component, then returns the piped output flow.
        /// </summary>
        public virtual async IAsyncEnumerable<TOutput> PipeFlow(IAsyncEnumerable<TInput> flow, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            // The default implementation is 1-1 dripping, while FlowMachines have a tailored and more efficient nInputs:nOutputs flowing implementation.
            await foreach(var t in flow) {
                await ConsumeDroplet(t);
                TOutput ret = await Drip();
                if (!IsFlowStarted || (stop?.Invoke(ret) ?? false) || --maxDroplets == 0) {
                    yield break;
                }
                yield return ret;
            }
        }

        public IAsyncEnumerable<TOutput> PipeFlow(IEnumerable<TInput> source, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return PipeFlow(source.ToAsyncEnumerable(), stop, maxDroplets);
        }

        public IAsyncEnumerable<TOutput> PipeFlow(Func<IAsyncEnumerable<TInput>> flowStarter, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return PipeFlow(flowStarter(), stop, maxDroplets);
        }

        /// <summary>
        /// Pipes an input flow into this component until it runs dry, then returns the output flow.
        /// </summary>
        public async IAsyncEnumerable<TOutput> KickstartFlow(IAsyncEnumerable<TInput> flow, Predicate<TOutput> pipeStop = null, int pipeMaxDroplets = 0, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            await foreach (var t in PipeFlow(flow, pipeStop, pipeMaxDroplets)) {
                yield return t;
            }
            await foreach (var t in Flow(stop, maxDroplets)) {
                yield return t;
            }
        }
        public IAsyncEnumerable<TOutput> KickstartFlow(IEnumerable<TInput> source, Predicate<TOutput> pipeStop = null, int pipeMaxDroplets = 0, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return KickstartFlow(source.ToAsyncEnumerable(), pipeStop, pipeMaxDroplets, stop, maxDroplets);
        }


    }
}

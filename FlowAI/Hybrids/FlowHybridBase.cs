using FlowAI.Consumers;
using FlowAI.Producers;
using System;
using System.Collections.Async;
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
        public abstract IAsyncEnumerator<bool> ConsumeFlow(IAsyncEnumerator<TInput> flow);

        /// <summary>
        /// Pipes an input flow into this component, then returns the piped output flow.
        /// </summary>
        public virtual IAsyncEnumerator<TOutput> PipeFlow(IAsyncEnumerator<TInput> flow, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            // The default implementation is 1-1 dripping, while FlowMachines have a tailored and more efficient nInputs:nOutputs flowing implementation.
            return new AsyncEnumerator<TOutput>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    await ConsumeDroplet(t);
                    TOutput ret = await Drip();
                    if (!IsFlowStarted || (stop?.Invoke(ret) ?? false) || --maxDroplets == 0)
                    {
                        yield.Break();
                    }
                    await yield.ReturnAsync(ret);
                });
            });
        }

        public IAsyncEnumerator<TOutput> PipeFlow(IEnumerable<TInput> source, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return PipeFlow(source.GetAsyncEnumerator(), stop, maxDroplets);
        }

        public IAsyncEnumerator<TOutput> PipeFlow(Func<IAsyncEnumerator<TInput>> flowStarter, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return PipeFlow(flowStarter(), stop, maxDroplets);
        }

        /// <summary>
        /// Pipes an input flow into this component until it runs dry, then returns the output flow.
        /// </summary>
        public IAsyncEnumerator<TOutput> KickstartFlow(IAsyncEnumerator<TInput> flow, Predicate<TOutput> pipeStop = null, int pipeMaxDroplets = 0, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return new AsyncEnumerator<TOutput>(async yield =>
            {
                await PipeFlow(flow, pipeStop, pipeMaxDroplets).ForEachAsync(async t =>
                {
                    await yield.ReturnAsync(t);
                });

                await Flow(stop, maxDroplets).ForEachAsync(async t =>
                {
                    await yield.ReturnAsync(t);
                });
            });
        }
        public IAsyncEnumerator<TOutput> KickstartFlow(IEnumerable<TInput> source, Predicate<TOutput> pipeStop = null, int pipeMaxDroplets = 0, Predicate<TOutput> stop = null, int maxDroplets = 0)
        {
            return KickstartFlow(source.GetAsyncEnumerator(), pipeStop, pipeMaxDroplets, stop, maxDroplets);
        }


    }
}

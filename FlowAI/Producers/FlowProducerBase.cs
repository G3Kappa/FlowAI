using System;
using System.Collections.Async;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI
{
    public abstract class FlowProducerBase<T> : IFlowProducer<T>
    {
        protected bool IsOpen { get; private set; }
        /// <summary>
        /// Called when this producer should start producing droplets.
        /// </summary>
        /// <returns>True if the faucet could be opened or was already open.</returns>
        public virtual async Task<bool> StartFlow() => await Task.Run(() => IsOpen = true);
        /// <summary>
        /// Called when this producer should stop producing droplets.
        /// </summary>
        /// <returns>True if the faucet could be closed or was already closed.</returns>
        public virtual async Task<bool> StaunchFlow() => await Task.Run(() => !(IsOpen = false));

        public virtual bool IsFlowStarted() => IsOpen;

        public FlowProducerBase()
        {
            IsOpen = true;
        }

        public abstract Task<T> Drip();
        public virtual IAsyncEnumerator<T> Flow(Predicate<T> stop = null, int maxDroplets=0)
        {
            return new AsyncEnumerator<T>(async yield =>
            {
                while(IsFlowStarted())
                {
                    T ret = await Drip();
                    await yield.ReturnAsync(ret);

                    if (--maxDroplets == 0 || stop != null && stop(ret))
                    {
                        yield.Break();
                    }
                }
            });
        }
    }
}

using FlowAI.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI.Producers
{
    public abstract class FlowProducerBase<T> : IFlowProducer<T>
    {
        protected bool IsOpen { get; private set; }
        /// <summary>
        /// Called when this producer should start producing droplets.
        /// </summary>
        /// <returns>True if the faucet could be opened or was already open.</returns>
        public virtual async Task<bool> StartFlow()
        {
            return await Task.Run(() => IsOpen = true);
        }

        /// <summary>
        /// Called when this producer should stop producing droplets.
        /// </summary>
        /// <returns>True if the faucet could be closed or was already closed.</returns>
        public virtual async Task<bool> StaunchFlow()
        {
            return await Task.Run(() => !(IsOpen = false));
        }

        public FlowInterruptedException<T> LastError { get; private set; }
        /// <summary>
        /// Called when this producer should break off the current flow.
        /// </summary>
        /// <param name="restart">If false, the flow must then be manually restarted to indicate that error handling took place.</param>
        public async Task<bool> InterruptFlow(FlowInterruptedException<T> reason = null, bool restart = false)
        {
            if(IsFlowStarted && await StaunchFlow())
            {
                //Flow().Dispose();

                LastError = reason ?? LastError;
                if(reason != null)
                {
#if !DEBUG
                    if(reason.Fatal) 
#endif
                    {
                        throw reason;
                    }
                }

                return restart && await StartFlow();
            }
            return false;
        }

        public virtual bool IsFlowStarted => IsOpen;

        public FlowProducerBase()
        {
            IsOpen = true;
        }

        public abstract Task<T> Drip();
        public virtual IAsyncEnumerable<T> Flow(Predicate<T> stop = null, int maxDroplets=0)
        {
            return Flow_Internal(this, stop, maxDroplets);
        }

        internal static async IAsyncEnumerable<T> Flow_Internal(FlowProducerBase<T> self, Predicate<T> stop = null, int maxDroplets = 0)
        {

            while (self.IsFlowStarted) {
                var ret = await self.Drip();
                if (self.IsOpen) {
                    yield return ret;
                }
                if (!self.IsOpen || --maxDroplets == 0 || (stop != null && stop(ret))) {
                    break;
                }
            }
        }
    }
}

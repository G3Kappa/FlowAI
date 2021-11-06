using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace FlowAI.Producers
{
    public interface IFlowProducer<T>
    {
        /// <summary>
        /// Retrieve a single datum (droplet) from the flow.
        /// </summary>
        Task<T> Drip();
        /// <summary>
        /// Continuously retrieve data from the flow until a stop condition is met.
        /// </summary>
        /// <param name="stop">The stop condition.</param>
        IAsyncEnumerable<T> Flow(Predicate<T> stop = null, int maxDroplets = 0);

        /// <summary>
        /// Check whether this producer is enabled.
        /// </summary>
        /// <returns>True if this producer can produce flow.</returns>
        bool IsFlowStarted { get; }
    }
}

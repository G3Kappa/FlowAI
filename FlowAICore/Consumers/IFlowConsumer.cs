using FlowAI.Producers;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace FlowAI.Consumers
{
    public interface IFlowConsumer<T>
    {
        /// <summary>
        /// Consumes a droplet from a producer.
        /// </summary>
        /// <returns>Whether the droplet was consumed.</returns>
        Task<bool> ConsumeDroplet(T droplet);
        /// <summary>
        /// Consumes a flow of droplets from a producer.
        /// </summary>
        /// <param name="flow">A sequence of droplets to consume.</param>
        /// <returns>Whether each droplet was consumed.</returns>
        IAsyncEnumerable<bool> ConsumeFlow(IAsyncEnumerable<T> flow);

    }
}

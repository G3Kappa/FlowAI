using System.Collections.Async;
using System.Threading.Tasks;


namespace FlowAI
{
    public interface IFlowConsumer<T>
    {
        /// <summary>
        /// Consumes a droplet from a producer.
        /// </summary>
        /// <returns>Whether the droplet was consumed.</returns>
        Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet);
        /// <summary>
        /// Consumes a flow of droplets from a producer.
        /// </summary>
        /// <param name="flow">A sequence of droplets to consume.</param>
        /// <returns>Whether each droplet was consumed.</returns>
        IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow);
    }
}

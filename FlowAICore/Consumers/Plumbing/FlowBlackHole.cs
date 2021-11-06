using FlowAI.Producers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Consumers.Plumbing
{
    /// <summary>
    /// Consumes everything while doing literally nothing and is always full. Perfect as a sink for data you don't care about.
    /// </summary>
    public class FlowBlackHole<T> : IFlowConsumer<T>
    {
        public FlowBlackHole() { }

        public async Task<bool> ConsumeDroplet(T droplet)
        {
            return await Task.Run(() => false);
        }

        public async IAsyncEnumerable<bool> ConsumeFlow(IAsyncEnumerable<T> flow)
        {
            await foreach (var droplet in flow) {
                yield return await ConsumeDroplet(droplet);
            }
        }
    }
}

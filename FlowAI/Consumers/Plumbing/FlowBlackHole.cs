using FlowAI.Producers;
using System.Collections.Async;
using System.Collections.Concurrent;
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

        public IAsyncEnumerator<bool> ConsumeFlow(IAsyncEnumerator<T> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    bool stored = await ConsumeDroplet(t);
                    await yield.ReturnAsync(stored);
                });
            });
        }
    }
}

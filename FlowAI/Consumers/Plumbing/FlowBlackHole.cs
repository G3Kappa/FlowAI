using System.Collections.Async;
using System.Collections.Concurrent;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Consumes everything while doing literally nothing and is always full. Perfect as a sink for data you don't care about.
    /// </summary>
    public class FlowBlackHole<T> : IFlowConsumer<T>
    {
        public FlowBlackHole() { }

        public async Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet)
        {
            return await Task.Run(() => false);
        }

        public IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    bool stored = await ConsumeDroplet(producer, t);
                    await yield.ReturnAsync(stored);
                });
            });
        }
    }
}

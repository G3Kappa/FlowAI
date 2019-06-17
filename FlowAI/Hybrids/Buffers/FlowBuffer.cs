﻿using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI
{

    /// <summary>
    /// Consumes and stores droplets until they are requested. Can be created with a fixed capacity, past which no more droplets will be stored.
    /// </summary>
    public class FlowBuffer<T> : FlowHybridBase<T>
    {
        protected ConcurrentQueue<T> Queue { get; private set; }
        public IReadOnlyCollection<T> Contents => Queue;
        public override bool IsFlowStarted() => base.IsFlowStarted() && Queue.Count > 0;

        public int Capacity { get; }

        public FlowBuffer(int capacity = 0) : base()
        {
            Capacity = capacity > 0 ? capacity : 0;
            Queue = new ConcurrentQueue<T>();
        }
        public override async Task<T> Drip()
        {
            return await Task.Run(() => {
                while (true)
                {
                    if (Queue.TryDequeue(out T ret)) return ret;
                }
            });
        }
        public override async Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet)
        {
            return await Task.Run(() =>
            {
                if(Capacity <= 0 || Queue.Count < Capacity)
                {
                    Queue.Enqueue(droplet);
                }
                return Queue.Count < Capacity;
            });
        }
        public override IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<T> producer, IAsyncEnumerator<T> flow)
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

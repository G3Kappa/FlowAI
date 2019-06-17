﻿using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// Like a regular junction, but droplets aren't copied and rather distributed exclusively to the first consumer until it is full, then to the second, and so on.
    /// It's considered full when the last consumer in the chain returns false.
    /// </summary>
    public class SequentialFlowInputJunction<T> : FlowInputJunction<T>
    {
        public int Current { get; private set; } = 0;
        public SequentialFlowInputJunction(params IFlowConsumer<T>[] consumers) : base(consumers) { }
        public override async Task<bool> ConsumeDroplet(IFlowProducer<T> producer, T droplet)
        {
            if (Consumers.Count == 0) return false;
            if (Current >= Consumers.Count)
            {
                Current = 0;
            }
            bool ret = await Consumers.ToArray()[Current].ConsumeDroplet(producer, droplet);
            if (!ret && ++Current == Consumers.Count)
            {
                return false;
            }
            return true;
        }
    }
}

using FlowAI.Producers;
using System.Threading.Tasks;


namespace FlowAI.Consumers.Plumbing
{
    /// <summary>
    /// Like a regular junction, but droplets aren't copied and rather evenly distributed among each consumer.
    /// It's considered full when all consumers return false after one cycle.
    /// </summary>
    public class SplittingFlowInputJunction<T> : FlowInputJunction<T>
    {
        public int Current { get; private set; } = 0;
        protected bool AllFalse { get; private set; } = true;

        public SplittingFlowInputJunction(params IFlowConsumer<T>[] consumers) : base(consumers) { }

        public override async Task<bool> ConsumeDroplet(T droplet)
        {
            if (Consumers.Count == 0) return false;
            if(Current >= Consumers.Count)
            {
                Current = 0;
                AllFalse = true;
            }
            bool ret = await Consumers.ToArray()[Current++].ConsumeDroplet(droplet);
            if(ret)
            {
                AllFalse = false;
            }
            else if(AllFalse && Current == Consumers.Count)
            {
                return false;
            }
            return true;
        }
    }
}

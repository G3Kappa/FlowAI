using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Producers.Sequences
{
    /// <summary>
    /// A producer that continously emits a sequence, repeating it when it ends.
    /// </summary>
    public class FlowSequence<T> : FlowProducerBase<T>
    {
        public IReadOnlyList<T> Sequence { get; protected set; }
        public int Current { get; protected set; } = 0;
        public override bool IsFlowStarted => base.IsFlowStarted && Sequence.Count > 0;

        public FlowSequence(IEnumerable<T> seq) : base()
        {
            Sequence = new ReadOnlyCollection<T>(seq.ToList());
        }

        public override async Task<T> Drip()
        {
            return await Task.Run(() =>
            {
                T ret = Sequence[Current++];
                if (Current >= Sequence.Count)
                {
                    Current = 0;
                }
                return ret;
            });
        }
    }
}

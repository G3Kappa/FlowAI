using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// A producer that continously emits a random sequence of fixed length containing a specific set of values that can be regenerated or kept when exhausted.
    /// </summary>
    public class RandomFlowSequence<T> : FlowSequence<T>
    {
        private Random Rng { get; }
        public IReadOnlyList<T> AllowedSymbols { get; protected set; }
        /// <summary>
        /// If true, the last sequence will be repeated instead of being generated anew when it's exhausted.
        /// </summary>
        public bool RepeatSameSequence { get; set; }

        private IReadOnlyList<T> GenerateSequence(int length)
        {
            if(RepeatSameSequence && Sequence != null && Sequence.Count > 0)
            {
                return Sequence;
            }

            var tmp = new List<T>();
            for (int i = 0; i < length; i++)
            {
                T symbol = AllowedSymbols[Rng.Next(AllowedSymbols.Count)];
                tmp.Add(symbol);
            }

            return new ReadOnlyCollection<T>(tmp);
        }

        /// <summary>
        /// A producer that continously emits a random sequence of fixed length containing a specific set of values that can be regenerated or kept when exhausted.
        /// </summary>
        /// <param name="symbols">The set of symbols to use in the generated sequences. Repeating the same symbol can be used as a way to created weighted sequences.</param>
        /// <param name="sequenceLength">The length of each generated sequence.</param>
        /// <param name="repeatSameSequence">If true, reuse the last generated sequence.</param>
        public RandomFlowSequence(IEnumerable<T> symbols, int sequenceLength, bool repeatSameSequence) : base(new T[] { })
        {
            Rng = new Random();
            AllowedSymbols = new ReadOnlyCollection<T>(symbols.ToList());
            RepeatSameSequence = repeatSameSequence;
            Sequence = GenerateSequence(sequenceLength);
        }

        public override async Task<T> Drip() => await Task.Run(() => {
            T ret = Sequence[Current++];
            if (Current >= Sequence.Count)
            {
                Current = 0;
                Sequence = GenerateSequence(Sequence.Count);
            }
            return ret;
        });
    }
}

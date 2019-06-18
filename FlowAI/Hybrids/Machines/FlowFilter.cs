using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlowAI
{
    /// <summary>
    /// Filters sequences from a flow, optionally piping the filtered droplets to another consumer.
    /// </summary>
    public class FlowFilter<T> : ChunkFlowMapper<T>
    {
        /// <summary>
        /// A filter maps a n-dimensional sequence to the empty sequence only when it matches
        /// </summary>
        private static Func<T[], T[]> FilterToMap(Predicate<T[]> filter) => buf => filter(buf) ? new T[] { } : buf;

        /// <summary>
        /// If non-null, filtered droplets will be piped away to this consumer.
        /// </summary>
        public IFlowConsumer<T> FilteredDropletsConsumer { get; set; }

        public FlowFilter(Predicate<T[]> filter, int chunkSize, IFlowConsumer<T> filterConsumer = null) : base(FilterToMap(filter), chunkSize)
        {
            FilteredDropletsConsumer = filterConsumer;
        }

        protected override T[] OnInputTransformed(T[] input, T[] output)
        {
            if (FilteredDropletsConsumer != null && output.Length == 0)
            {
                Task.Run(async () =>
                {
                    await FilteredDropletsConsumer.ConsumeFlow(InputBuffer, input.GetAsyncEnumerator()).Collect();
                });
            }

            return base.OnInputTransformed(input, output);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FlowAI
{
    /// <summary>
    /// Filters sequences from a flow, optionally piping the filtered droplets to another consumer.
    /// </summary>
    public class FlowFilter<T> : FlowMapper<T>
    {
        /// <summary>
        /// A filter maps a n-dimensional sequence to the empty sequence only when it matches
        /// </summary>
        private static Func<T[], T[]> FilterToMap(Predicate<T[]> filter)
        {
            return buf => filter(buf) ? new T[] { } : buf;
        }

        /// <summary>
        /// If non-null, filtered droplets will be piped to this consumer.
        /// </summary>
        public IFlowConsumer<T> FilterSink { get; set; }

        public FlowFilter(Predicate<T[]> filter, int chunkSize, IFlowConsumer<T> filterConsumer = null) : base(FilterToMap(filter), chunkSize)
        {
            FilterSink = filterConsumer;
        }

        protected override T[] OnInputTransformed(T[] input, T[] output)
        {
            if (FilterSink != null && output.Length == 0)
            {
                Task.Run(async () =>
                {
                    await FilterSink.ConsumeFlow(InputBuffer, input.GetAsyncEnumerator()).Collect();
                });
            }

            return base.OnInputTransformed(input, output);
        }

    }
}

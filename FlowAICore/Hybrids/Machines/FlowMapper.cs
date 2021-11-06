using FlowAI.Producers;
using System;
using System.Collections.Concurrent;
using System.Linq;


namespace FlowAI.Hybrids.Machines
{
    /// <summary>
    /// A machine that maps chunks of droplets as it consumes them. If you want something that can change the type of the output, consider a FlowTransformer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FlowMapper<T> : FlowTransformer<T, T>
    {
        public FlowMapper(Func<T[], T[]> mapping, int chunkSize) : base(mapping, (a, b) => !a.SequenceEqual(b), chunkSize)
        {
        }
    }
}

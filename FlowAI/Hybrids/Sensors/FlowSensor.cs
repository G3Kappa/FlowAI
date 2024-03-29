﻿using FlowAI.Hybrids.Buffers;
using FlowAI.Producers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Sensors
{
    /// <summary>
    /// A smart buffer that outputs a on/off value when its content match a predefined sequence.
    /// </summary>
    public class FlowSensor<T> : CyclicFlowBuffer<T>
    {
        public IReadOnlyList<T> Sequence { get; }

        public T OnValue { get; }
        public T OffValue { get; }
        public T Value { get; protected set; }

        public FlowSensor(IList<T> seq, T onValue, T offValue) : base(seq.Count)
        {
            Sequence = new ReadOnlyCollection<T>(seq);
            OnValue = onValue;
            OffValue = offValue;
        }

        public override async Task<bool> ConsumeDroplet(T droplet)
        {
            bool ret = await base.ConsumeDroplet(droplet);
            Value = Contents.SequenceEqual(Sequence) ? OnValue : OffValue;
            return ret;
        }
    }
}

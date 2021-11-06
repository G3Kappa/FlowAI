using System.Collections;
using System.Collections.Generic;


namespace FlowAI.Hybrids.Buffers
{
    /// <summary>
    /// A buffer that only stores the maximum value that passed through it before the output was collected.
    /// </summary>
    public class DropletBuffer<T> : CyclicFlowBuffer<T>
    {
        public DropletBuffer() : base(1) { }
    }
}

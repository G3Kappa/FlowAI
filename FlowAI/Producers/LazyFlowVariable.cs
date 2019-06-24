using System;
using System.Threading.Tasks;


namespace FlowAI.Producers
{
    /// <summary>
    /// A FlowVariable whose value is evaluated only when requested.
    /// </summary>
    public class LazyFlowVariable<T> : FlowProducerBase<T>
    {
        public Func<T> Value { get; set; }
        public LazyFlowVariable(Func<T> value) : base()
        {
            Value = value;
        }

        public override async Task<T> Drip()
        {
            return await Task.Run(Value);
        }
    }
}

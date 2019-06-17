using System.Threading.Tasks;


namespace FlowAI
{
    /// <summary>
    /// A simple producer that continuously emits a constant.
    /// </summary>
    public class FlowConstant<T> : FlowProducerBase<T>
    {
        public T Value { get; }
        public FlowConstant(T value) : base()
        {
            Value = value;
        }

        public override async Task<T> Drip() => await Task.Run(() => Value);
    }
}

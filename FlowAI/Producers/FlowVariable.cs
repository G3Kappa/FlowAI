using System.Threading.Tasks;


namespace FlowAI.Producers
{
    /// <summary>
    /// A simple producer that continuously emits a value.
    /// </summary>
    public class FlowVariable<T> : FlowProducerBase<T>
    {
        public T Value { get; set; }
        public FlowVariable(T value) : base()
        {
            Value = value;
        }

        public override async Task<T> Drip()
        {
            return await Task.Run(() => Value);
        }
    }
}

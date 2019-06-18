using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowAI.Exceptions
{
    /// <summary>
    /// Represents a piping error that would lead to a deadlock if not raised.
    /// </summary>
    public class FlowInterruptedException<T> : Exception
    {
        public IFlowProducer<T> Sender { get; }
        public string Context { get; }

        public bool Fatal { get; }

        public FlowInterruptedException(IFlowProducer<T> flowProducer, string context, bool fatal, Exception inner = null) 
            : base($"The flow was interrupted prematurely{(fatal ? " in a fatal way" : "")}. Context: {flowProducer.GetType().Name}::{context}", inner)
        {
            Sender = flowProducer;
            Fatal = fatal;
        }
    }
}

using FlowAI.Hybrids.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace FlowAI.Hybrids.Machines
{

    /// <summary>
    /// A machine that transforms and changes the type of chunks of droplets as it consumes them. Like a generalized FlowMapper, but it requires a few extra parameters in order to work.
    /// </summary>
    public class FlowTransformer<TInput, TOutput> : FlowMachineBase<TInput, TOutput>
    {
        public Func<TInput[], TOutput[]> Map { get; protected set; }
        public Func<TInput[], TOutput[], bool> ConsumeIf { get; protected set; }
        public int ChunkSize { get; protected set; }
        public enum InputBufferFullStrategy
        {
            FlushToOutput,
            Drip,
            Empty
        }
        public InputBufferFullStrategy InputBufferStrategy { get; set; }

        public FlowTransformer(Func<TInput[], TOutput[]> mapping, Func<TInput[], TOutput[], bool> consumeIf, int chunkSize) : base(chunkSize, 0)
        {
            Map = mapping;
            InputBufferStrategy = InputBufferFullStrategy.FlushToOutput;
            ChunkSize = chunkSize;
            ConsumeIf = consumeIf;
        }

        public override async Task Update(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf)
        {
            TInput[] oldContents = inBuf.Contents.ToArray();
            TOutput[] mapped = Map(oldContents);
            if (ConsumeIf(oldContents, mapped))
            {
                await inBuf.Flow(maxDroplets: inBuf.Capacity).Collect();
                mapped = OnInputTransformed(oldContents, mapped);
                await outBuf.ConsumeFlow(mapped.GetAsyncEnumerator()).Collect();
            }
            else if (inBuf.Full)
            {
                switch(InputBufferStrategy)
                {
                    default:
                    case InputBufferFullStrategy.FlushToOutput:
                        await inBuf.Drip();
                        await outBuf.ConsumeDroplet(mapped[0]);
                        break;
                    case InputBufferFullStrategy.Empty:
                        while(!inBuf.Empty)
                        {
                            await inBuf.Drip();
                        }
                        break;
                    case InputBufferFullStrategy.Drip:
                        await inBuf.Drip();
                        break;
                }
            }
        }

        public override async Task Flush(FlowBuffer<TInput> inBuf, FlowBuffer<TOutput> outBuf)
        {
            if(outBuf is FlowBuffer<TInput> buf)
            {
                await buf.ConsumeFlow(inBuf.Flow()).Collect();
            }
            else
            {
                await outBuf.ConsumeFlow(Map((await inBuf.Flow().Collect()).ToArray()).GetAsyncEnumerator()).Collect();
            }
        }

        /// <summary>
        /// Called whenever a mapping operation takes place, right before the output buffer loads it.
        /// </summary>
        protected virtual TOutput[] OnInputTransformed(TInput[] input, TOutput[] output)
        {
            return output;
        }
    }
}

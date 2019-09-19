using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlowAI.Producers;

namespace FlowAI.Hybrids.Adapters
{
    public class FlowAdapter<TStream, TDroplet> : FlowHybridBase<TDroplet, TDroplet>, IDisposable
        where TStream : Stream
    {
        public TStream SourceStream { get; protected set; }
        public int ChunkSize { get; protected set; }
        public Func<byte[], TDroplet> ReadAdapter { get; protected set; }
        public Func<TDroplet, byte[]> WriteAdapter { get; protected set; }

        protected virtual bool DataAvailable(TStream stream)
        {
            try
            {
                return stream.CanRead && stream.Position < stream.Length;
            }
            catch(NotSupportedException)
            {
                return false;
            }
        }

        public FlowAdapter(TStream source, Func<byte[], TDroplet> readAdapter, Func<TDroplet, byte[]> writeAdapter, int chunkSize) : base()
        {
            SourceStream = source;
            ChunkSize = chunkSize > 0 ? chunkSize : 1;
            ReadAdapter = readAdapter;
            WriteAdapter = writeAdapter;
        }

        public override async Task<bool> ConsumeDroplet(TDroplet droplet)
        {
            if (SourceStream.CanWrite)
            {
                byte[] buf = WriteAdapter(droplet);
                try
                {
                    await SourceStream.WriteAsync(buf, 0, ChunkSize);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

        public override IAsyncEnumerator<bool> ConsumeFlow(IAsyncEnumerator<TDroplet> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    bool stored = await ConsumeDroplet(t);
                    await yield.ReturnAsync(stored);
                });
            });
        }

        public override async Task<TDroplet> Drip()
        {
            {
                byte[] buf = new byte[ChunkSize];
                try
                {
                    if (DataAvailable(SourceStream) && await SourceStream.ReadAsync(buf, 0, ChunkSize) > 0)
                    {
                        return ReadAdapter(buf);
                    }
                }
                catch { /* Fall down to InterruptFlow() */ }
            }
            await InterruptFlow(); return default;
        }

        public virtual void Dispose()
        {
            SourceStream?.Dispose();
            SourceStream = null;
        }
    }
}

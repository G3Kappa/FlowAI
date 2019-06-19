using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlowAI.Producers;

namespace FlowAI.Hybrids.Adapters
{
    public class FlowAdapter<TStream, TDroplet> : FlowHybridBase<TDroplet>, IDisposable
        where TStream : Stream
    {
        public TStream SourceStream { get; }
        public int ChunkSize { get; }
        public Func<byte[], TDroplet> ReadAdapter { get; protected set; }
        public Func<TDroplet, byte[]> WriteAdapter { get; protected set; }

        public FlowAdapter(TStream source, Func<byte[], TDroplet> readAdapter, Func<TDroplet, byte[]> writeAdapter, int chunkSize) : base()
        {
            SourceStream = source;
            ChunkSize = chunkSize > 0 ? chunkSize : 1;
            ReadAdapter = readAdapter;
            WriteAdapter = writeAdapter;
        }

        public override async Task<bool> ConsumeDroplet(IFlowProducer<TDroplet> producer, TDroplet droplet)
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

        public override IAsyncEnumerator<bool> ConsumeFlow(IFlowProducer<TDroplet> producer, IAsyncEnumerator<TDroplet> flow)
        {
            return new AsyncEnumerator<bool>(async yield =>
            {
                await flow.ForEachAsync(async t =>
                {
                    bool stored = await ConsumeDroplet(producer, t);
                    await yield.ReturnAsync(stored);
                });
            });
        }

        public override async Task<TDroplet> Drip()
        {
            if(SourceStream.CanRead)
            {
                byte[] buf = new byte[ChunkSize];
                try
                {
                    if (await SourceStream.ReadAsync(buf, 0, ChunkSize) > 0)
                    {
                        return ReadAdapter(buf);
                    }
                }
                catch { /* Fall down to InterruptFlow() */ }
            }
            await InterruptFlow(); return default;
        }

        public void Dispose()
        {
            SourceStream?.Dispose();
        }
    }

    public class FileStreamFlowAdapter : FlowAdapter<FileStream, char>
    {
        public FileStreamFlowAdapter(FileStream source, Encoding enc) : base(source, null, null, 1)
        {
            WriteAdapter = AdaptChars(enc);
            ReadAdapter = AdaptBytes(enc);
        }
        protected virtual Func<byte[], char> AdaptBytes(Encoding enc)
        {
            return packet => enc.GetChars(packet)[0];
        }

        protected virtual Func<char, byte[]> AdaptChars(Encoding enc)
        {
            return packet => enc.GetBytes(new[] { packet });
        }
    }
}

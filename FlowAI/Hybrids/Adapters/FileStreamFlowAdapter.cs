using System;
using System.IO;

namespace FlowAI.Hybrids.Adapters
{
    public class FileStreamFlowAdapter<TDroplet> : FlowAdapter<FileStream, TDroplet>
    {
        public FileStreamFlowAdapter(FileStream fs, Func<byte[], TDroplet> readAdapter, Func<TDroplet, byte[]> writeAdapter, int chunkSize)
            : base(fs, readAdapter, writeAdapter, chunkSize)
        {

        }
    }
}

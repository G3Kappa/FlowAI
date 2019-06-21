using System;
using System.IO;
using System.Text;

namespace FlowAI.Hybrids.Adapters
{
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

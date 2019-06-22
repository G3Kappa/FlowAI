using System.IO;
using System.Text;

namespace FlowAI.Hybrids.Adapters
{
    public class FileStreamFlowAdapterString : FlowAdapter<FileStream, string>
    {
        public FileStreamFlowAdapterString(FileStream fs, Encoding enc, int chunkSize)
            : base(fs, null, null, chunkSize)
        {
            WriteAdapter = packet => enc.GetBytes(packet);
            ReadAdapter = packet => new string(enc.GetChars(packet));
        }
    }
}

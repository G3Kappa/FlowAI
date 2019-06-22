using System.IO;
using System.Text;

namespace FlowAI.Hybrids.Adapters
{
    public class FileStreamFlowAdapterChar : FlowAdapter<FileStream, char>
    {
        public FileStreamFlowAdapterChar(FileStream fs, Encoding enc)
            : base(fs, null, null, 1)
        {
            WriteAdapter = packet => enc.GetBytes(new[] { packet });
            ReadAdapter = packet => enc.GetChars(packet)[0];
        }
    }
}

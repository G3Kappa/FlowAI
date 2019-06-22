using System;
using System.Net;
using System.Text;

namespace FlowAI.Hybrids.Adapters
{
    /// <summary>
    /// Reads data from a network stream as a series of individual strings of the given length and of the specified encoding.
    /// </summary>
    public class NetworkStreamFlowAdapterString : NetworkStreamFlowAdapter<string>
    {
        public NetworkStreamFlowAdapterString(IPEndPoint endPoint, Encoding enc, int chunkSize)
            : base(endPoint, null, null, chunkSize)
        {
            WriteAdapter = AdaptString(enc);
            ReadAdapter = AdaptBytes(enc);
        }
        protected virtual Func<byte[], string> AdaptBytes(Encoding enc)
        {
            return packet => new string(enc.GetChars(packet));
        }
        protected virtual Func<string, byte[]> AdaptString(Encoding enc)
        {
            return packet => enc.GetBytes(packet);
        }
    }
}

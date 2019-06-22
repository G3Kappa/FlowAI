using System;
using System.Net;
using System.Text;

namespace FlowAI.Hybrids.Adapters
{
    /// <summary>
    /// Reads data from a network stream as a series of individual characters of the specified encoding.
    /// </summary>
    public class NetworkStreamFlowAdapterChar : NetworkStreamFlowAdapter<char>
    {
        public NetworkStreamFlowAdapterChar(IPEndPoint endPoint, Encoding enc)
            : base(endPoint, null, null, 1)
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

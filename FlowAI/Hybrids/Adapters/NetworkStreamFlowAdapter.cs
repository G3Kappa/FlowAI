using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FlowAI.Hybrids.Adapters
{
    public class NetworkStreamFlowAdapter : FlowAdapter<NetworkStream, char>
    {
        public IPEndPoint EndPoint { get; }
        public Socket Socket { get; }

        public NetworkStreamFlowAdapter(IPEndPoint endPoint, Encoding enc) : base(null, null, null, 1)
        {
            EndPoint = endPoint;
            Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            WriteAdapter = AdaptChars(enc);
            ReadAdapter = AdaptBytes(enc);

            Task.Run(StartFlow).Wait();
        }

        public override async Task<bool> StartFlow()
        {
            if(!Socket.Connected)
            {
                await Socket.ConnectAsync(EndPoint);
                SourceStream = new NetworkStream(Socket, true)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
            }
            return await base.StartFlow();
        }

        public override Task<bool> StaunchFlow()
        {
            if (Socket.Connected)
            {
                Socket.Disconnect(true);
                SourceStream.Dispose();
                SourceStream = null;
            }
            return base.StaunchFlow();
        }
        public override void Dispose()
        {
            Task.Run(StaunchFlow).Wait();
            Socket.Dispose();
            base.Dispose();
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

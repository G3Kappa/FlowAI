using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FlowAI.Hybrids.Adapters
{
    /// <summary>
    /// An intermediate interface between a FlowAdapter and a more specialized type such as NetworkStreamFlowAdapterChar. 
    /// This lets you define the actual mapping between the bytes you read from a stream and the bytes you write to it.
    /// </summary>
    public class NetworkStreamFlowAdapter<TDroplet> : FlowAdapter<NetworkStream, TDroplet>
    {
        public IPEndPoint EndPoint { get; }
        public Socket Socket { get; }

        public NetworkStreamFlowAdapter(IPEndPoint endPoint, Func<byte[], TDroplet> readAdapter, Func<TDroplet, byte[]> writeAdapter, int chunkSize) 
            : base(null, readAdapter, writeAdapter, chunkSize)
        {
            EndPoint = endPoint;
            Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Task.Run(StartFlow).Wait();
        }

        // Needs to be overridden as NetworkStreams don't support .Position and .Length
        protected override bool DataAvailable(NetworkStream stream)
        {
            return stream.CanRead && stream.DataAvailable;
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
    }
}

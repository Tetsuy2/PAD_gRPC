using System.Net.Http;
using Grpc.Net.Client;

namespace Broker.Models
{
    public class Connection
    {
        public Connection(string address, string topic)
        {
            Address = address;
            Topic = topic;

            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10)
            };

            Channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = handler,                         // 👈 important pentru h2c stabil
                MaxReceiveMessageSize = 4 * 1024 * 1024,
                MaxSendMessageSize = 4 * 1024 * 1024
            });
        }

        public string Address { get; }
        public string Topic { get; }
        public GrpcChannel Channel { get; }

        public bool IsAlive() => Channel.State != Grpc.Core.ConnectivityState.Shutdown;
    }
}
 
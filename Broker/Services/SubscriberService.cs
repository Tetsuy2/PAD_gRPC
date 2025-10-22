using Broker.Models;
using Broker.Services.Interfaces;
using Grpc.Core;
using GrpcAgent;
using Grpc.Net.Client;

namespace Broker.Services
{
    public class SubscriberService : Subscriber.SubscriberBase
    {
        private readonly IConnectionStorageService _connections;
        private readonly IRetainedStore _retained;

        public SubscriberService(IConnectionStorageService connections, IRetainedStore retained)
        {
            _connections = connections;
            _retained = retained;
        }

        public override Task<SubscribeReply> Subscribe(SubscribeRequest request, ServerCallContext context)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.Topic))
                    return Task.FromResult(new SubscribeReply { IsSuccess = false, Error = "Invalid request" });

                var topic = request.Topic.Trim().ToLowerInvariant();
                var conn = new Connection(request.Address.Trim(), topic);
                _connections.Add(conn);
                Console.WriteLine($"[BROKER] Subscribed: {conn.Address} -> {conn.Topic}");

                // If we have a retained message for this topic, push it once immediately
                if (_retained.TryGet(topic, out var last) && last is not null)
                {
                    try
                    {
                        var client = new Notifier.NotifierClient(conn.Channel);
                        var reply = client.Notify(new NotifyRequest { Content = last.Content }, deadline: DateTime.UtcNow.AddSeconds(3));
                        Console.WriteLine($"[BROKER] Retained delivered to {conn.Address} -> {reply.IsSuccess}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[BROKER] Retained deliver failed to {conn.Address}: {ex.Message}");
                    }
                }

                return Task.FromResult(new SubscribeReply { IsSuccess = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new SubscribeReply { IsSuccess = false, Error = ex.Message });
            }
        }
    }
}

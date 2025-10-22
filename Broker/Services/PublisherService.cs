using Broker.Models;
using Broker.Services.Interfaces;
using Grpc.Core;
using GrpcAgent;
using System.Text.Json;

namespace Broker.Services
{
    public class PublisherService : Publisher.PublisherBase
    {
        private readonly IMessageStorageService _storage;
        private readonly IXmlValidator _xmlValidator;
        private readonly IRetainedStore _retained;

        public PublisherService(IMessageStorageService storage, IXmlValidator xmlValidator, IRetainedStore retained)
        {
            _storage = storage;
            _xmlValidator = xmlValidator;
            _retained = retained;
        }

        public override Task<PublishReply> PublishMessage(PublishRequest request, ServerCallContext context)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Topic) || string.IsNullOrWhiteSpace(request.Content))
                    return Task.FromResult(new PublishReply { IsSuccess = false, Error = "Invalid request" });

                var topic = request.Topic.Trim().ToLowerInvariant();
                var content = request.Content.Trim();
                var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "" : request.ContentType.Trim().ToLowerInvariant();
                var mode = request.Mode == DeliveryMode.Unicast ? "UNICAST" : "MULTICAST";

                if (contentType == "application/xml") _xmlValidator.Validate(content);
                else if (contentType == "application/json") JsonDocument.Parse(content);

                var message = new Message(topic, content, contentType, mode);

                // enqueue for normal delivery
                _storage.Add(message);

                // also retain the last message per topic for late subscribers
                _retained.Set(topic, message);

                Console.WriteLine($"[BROKER] Enqueued {mode} msg {message.MessageId} for topic '{topic}'.");
                return Task.FromResult(new PublishReply { IsSuccess = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new PublishReply { IsSuccess = false, Error = ex.Message });
            }
        }
    }
}

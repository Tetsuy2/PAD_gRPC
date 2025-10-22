namespace Broker.Models
{
    public class Message
    {
        public Message(string topic, string content, string contentType, string mode)
        {
            MessageId = Guid.NewGuid().ToString("N");
            Topic = topic;
            Content = content;
            ContentType = contentType;
            Mode = mode;            // "UNICAST" | "MULTICAST"
            TimestampUtc = DateTime.UtcNow;
            Deliveries = 0;
        }

        public string MessageId { get; }
        public string Topic { get; }
        public string Content { get; }
        public string ContentType { get; }
        public string Mode { get; }
        public DateTime TimestampUtc { get; }
        public int Deliveries { get; set; }
    }
}

namespace Common
{
    public static class EndpointsConstants
    {
        // Broker is HTTPS + HTTP/2
        public const string BrokerAddress = "https://localhost:5001";
        // Receiver hosts its own gRPC endpoint on a local ephemeral HTTP port
        public const string SubscribersAddress = "http://127.0.0.1:0";
    }
}

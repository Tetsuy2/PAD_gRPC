using Grpc.Net.Client;
using Proto;

static string Arg(string[] args, string key, string def = "")
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}

static void Show()
{
    Console.WriteLine("""
Usage:
  dotnet run -- --broker http://127.0.0.1:7001 --type OrderCreated --subject orders.new --payload "{\"orderId\":123}"
""");
}

if (args.Length == 0) { Show(); return; }

var brokerAddr = Arg(args, "--broker", "http://127.0.0.1:7001");
var type = Arg(args, "--type", "Test");
var subject = Arg(args, "--subject", "orders.new");
var payload = Arg(args, "--payload", "{\"hello\":\"world\"}");

// HTTP/2 without TLS (dev)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

using var chan = GrpcChannel.ForAddress(brokerAddr);
var client = new Broker.BrokerClient(chan);

var res = await client.PublishAsync(new PublishRequest
{
    Type = type,
    Subject = subject,
    Payload = payload
});

Console.WriteLine($"[gRPC Sender] Ack: {res.Ok} {(string.IsNullOrEmpty(res.Error) ? "" : res.Error)}");

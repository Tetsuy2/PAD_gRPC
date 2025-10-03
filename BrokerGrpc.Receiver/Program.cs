using System.Text.Json;
using Grpc.Net.Client;
using Proto;

static string Arg(string[] args, string key, string def = "")
{
    var i = Array.IndexOf(args, key);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
}
static bool Has(string[] args, string key) => Array.IndexOf(args, key) >= 0;

static void Show()
{
    Console.WriteLine("""
Usage:
  dotnet run --project .\BrokerGrpc.Receiver\BrokerGrpc.Receiver.csproj -- \
    --broker http://127.0.0.1:7001 --subject S [--inbox D:\path\to\inbox]

Notes:
  --inbox  folder where inbox.jsonl will be written (default: .\data\inbox_grpc)
""");
}

if (args.Length == 0) { Show(); return; }

var brokerAddr = Arg(args, "--broker", "http://127.0.0.1:7001");
var subject = Arg(args, "--subject", "demo.test");
var inboxDir = Arg(args, "--inbox", Path.Combine(AppContext.BaseDirectory, "data", "inbox_grpc"));

Directory.CreateDirectory(inboxDir);
var jsonlPath = Path.Combine(inboxDir, "inbox.jsonl");

// HTTP/2 without TLS (dev)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

using var ch = GrpcChannel.ForAddress(brokerAddr);
var client = new Broker.BrokerClient(ch);

using var call = client.Subscribe(new SubscribeRequest { Subject = subject });
Console.WriteLine($"[gRPC Receiver] subscribed to '{subject}' @ {brokerAddr}");
Console.WriteLine($"[gRPC Receiver] inbox folder: {inboxDir}");
Console.WriteLine($"[gRPC Receiver] saving JSONL to: {jsonlPath}");

try
{
    while (await call.ResponseStream.MoveNext(default))
    {
        var env = call.ResponseStream.Current;
        Console.WriteLine($"[gRPC Receiver] {env.Type}/{env.Subject} -> {env.Payload}");

        var json = JsonSerializer.Serialize(new
        {
            env.Type,
            env.Subject,
            env.Payload,
            env.Id,
            env.TimestampUnix
        });
        await File.AppendAllTextAsync(jsonlPath, json + Environment.NewLine);
    }
}
catch (TaskCanceledException)
{
    // normal on shutdown
}

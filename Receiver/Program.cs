using System.Linq;
using Common;
using Grpc.Net.Client;
using GrpcAgent;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Bind to an ephemeral local port like before…
builder.WebHost.UseUrls(EndpointsConstants.SubscribersAddress);

// ✅ …but force HTTP/2 for that endpoint so gRPC calls succeed
builder.WebHost.ConfigureKestrel(o =>
{
    o.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<Receiver.Services.NotificationService>();
app.MapGet("/", () => "Receiver ready.");

app.Start();

// Discover the actual bound address (http://127.0.0.1:<port>)
var server = app.Services.GetRequiredService<IServer>();
var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
Console.WriteLine($"[RECEIVER] Listening at: {address}");

string ReadTopic()
{
    while (true)
    {
        Console.Write("Topic: ");
        var t = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(t)) return t;
        Console.WriteLine("Please enter a non-empty topic.");
    }
}

var topic = Environment.GetEnvironmentVariable("RECEIVER_TOPIC");
topic = string.IsNullOrWhiteSpace(topic) ? ReadTopic() : topic.Trim().ToLowerInvariant();

try
{
    // Broker is HTTPS/HTTP2
    var ch = GrpcChannel.ForAddress(EndpointsConstants.BrokerAddress);
    var client = new Subscriber.SubscriberClient(ch);
    var reply = client.Subscribe(new SubscribeRequest { Topic = topic, Address = address });
    Console.WriteLine($"[RECEIVER] Subscribe -> {reply.IsSuccess} {(string.IsNullOrEmpty(reply.Error) ? "" : reply.Error)}");
}
catch (Exception ex)
{
    Console.WriteLine($"[RECEIVER] Subscribe error: {ex.Message}");
}

Console.WriteLine("Press Ctrl+C to exit.");
await app.WaitForShutdownAsync();

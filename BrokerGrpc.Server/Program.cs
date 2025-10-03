using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BrokerGrpc.BrokerState>();

// Configure port via env var BROKER_PORT or appsettings "Broker:Port"
var envPort = Environment.GetEnvironmentVariable("BROKER_PORT");
var port = int.TryParse(envPort, out var p)
    ? p
    : builder.Configuration.GetValue<int>("Broker:Port", 7001);

builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(port, o => o.Protocols = HttpProtocols.Http2); // gRPC over HTTP/2 (no TLS in dev)
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<BrokerGrpc.BrokerService>();
app.MapGet("/", () => $"gRPC broker running on http://0.0.0.0:{port} (HTTP/2)");

Console.WriteLine($"[gRPC] listening on http://0.0.0.0:{port} (HTTP/2, insecure)");
await app.RunAsync();

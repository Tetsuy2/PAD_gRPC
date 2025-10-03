using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// Fixed port 7001 (simple for the moment)
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(7001, o => o.Protocols = HttpProtocols.Http2); // gRPC over HTTP/2
});

builder.Services.AddGrpc();

var app = builder.Build();

app.MapGrpcService<BrokerGrpc.BrokerService>();
app.MapGet("/", () => "gRPC broker running on http://0.0.0.0:7001 (HTTP/2)");

Console.WriteLine("[gRPC] listening on http://0.0.0.0:7001 (HTTP/2, insecure)");
await app.RunAsync();

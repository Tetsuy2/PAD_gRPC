using Broker.Services;
using Broker.Services.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, listen =>
    {
        listen.Protocols = HttpProtocols.Http2;
        listen.UseHttps();
    });
});

builder.Services.AddSingleton<IMessageStorageService, MessageStorageService>();
builder.Services.AddSingleton<IConnectionStorageService, ConnectionStorageService>();
builder.Services.AddSingleton<IRouterService, RouterService>();
builder.Services.AddSingleton<IXmlValidator, XmlValidator>();
builder.Services.AddSingleton<IRetainedStore, RetainedStore>();   // <— NEW
builder.Services.AddHostedService<SenderWorker>();

var app = builder.Build();

app.MapGrpcService<PublisherService>();
app.MapGrpcService<SubscriberService>();
app.MapGet("/health", () => "OK");
app.MapGet("/", () => "Use a gRPC client.");

app.Run();

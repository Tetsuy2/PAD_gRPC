using System;
using Common;
using Grpc.Net.Client;
using GrpcAgent;

static string ReadTopic()
{
    while (true)
    {
        Console.Write("Topic (or 'exit'): ");
        var t = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (t == "exit") return t;
        if (!string.IsNullOrWhiteSpace(t)) return t;
        Console.WriteLine("Topic cannot be empty.");
    }
}

static string ReadContent()
{
    while (true)
    {
        Console.Write("Content: ");
        var c = Console.ReadLine() ?? "";
        if (!string.IsNullOrWhiteSpace(c)) return c;
        Console.WriteLine("Content cannot be empty.");
    }
}

static string ReadContentType()
{
    while (true)
    {
        Console.Write("ContentType [1=JSON, 2=XML, 3=Text]: ");
        var k = (Console.ReadLine() ?? "").Trim();
        if (k == "1") return "application/json";
        if (k == "2") return "application/xml";
        if (k == "3" || k == "") return "";
        Console.WriteLine("Please choose 1, 2 or 3.");
    }
}

static DeliveryMode ReadMode()
{
    while (true)
    {
        Console.Write("Mode [1=Multicast, 2=Unicast]: ");
        var k = (Console.ReadLine() ?? "").Trim();
        if (k == "1" || k == "") return DeliveryMode.Multicast;
        if (k == "2") return DeliveryMode.Unicast;
        Console.WriteLine("Please choose 1 or 2.");
    }
}

var channel = GrpcChannel.ForAddress(EndpointsConstants.BrokerAddress);
var client = new Publisher.PublisherClient(channel);

Console.WriteLine("=== Publisher ===");

while (true)
{
    var topic = ReadTopic();
    if (topic == "exit") break;

    var content = ReadContent();
    var contentType = ReadContentType();
    var mode = ReadMode();

    if (contentType == "application/json" && !(content.StartsWith('{') || content.StartsWith('[')))
        Console.WriteLine("Tip: JSON usually starts with { } or [ ].");
    if (contentType == "application/xml" && !content.TrimStart().StartsWith('<'))
        Console.WriteLine("Tip: XML usually starts with a <root> element.");

    try
    {
        var reply = await client.PublishMessageAsync(new PublishRequest
        {
            Topic = topic,
            Content = content,
            ContentType = contentType,
            Mode = mode
        });

        Console.WriteLine($"Publish -> {(reply.IsSuccess ? "OK" : "FAILED")} {(string.IsNullOrEmpty(reply.Error) ? "" : "| " + reply.Error)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Publish error: {ex.Message}");
    }
    Console.WriteLine();
}

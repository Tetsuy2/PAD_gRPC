using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Grpc.Net.Client;
using Proto;

// ---------- CLI helpers ----------
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
    --broker http://127.0.0.1:7001 --subject S \
    [--inbox D:\path\to\inbox] [--save-xml] [--xsd D:\path\to\envelope.xsd]

Notes:
  --inbox     folder where inbox.jsonl and *.xml will be written (default: .\data\inbox_grpc)
  --save-xml  also save each message as XML (namespaced) besides JSONL
  --xsd       if given, validate the XML against the XSD and print result
""");
}

// ---------- XML helpers ----------
const string EnvelopeNs = "urn:broker:envelope:v1";

static string EnvelopeToXml(Envelope e)
{
    XNamespace ns = EnvelopeNs;
    var dt = e.TimestampUnix != 0
        ? DateTimeOffset.FromUnixTimeSeconds(e.TimestampUnix).UtcDateTime
        : DateTime.UtcNow;

    var doc = new XElement(ns + "Envelope",
        new XElement(ns + "Type", e.Type ?? ""),
        new XElement(ns + "Subject", e.Subject ?? ""),
        new XElement(ns + "Payload", e.Payload ?? ""),
        new XElement(ns + "Timestamp", dt.ToString("o")),
        new XElement(ns + "Id", string.IsNullOrWhiteSpace(e.Id) ? Guid.NewGuid().ToString() : e.Id)
    );

    return doc.ToString(SaveOptions.DisableFormatting);
}

static bool TryValidate(string xml, string xsdPath, out string? error)
{
    string? validationError = null;

    var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
    settings.Schemas.Add(EnvelopeNs, xsdPath); // bind schema to same namespace
    settings.ValidationEventHandler += (_, a) =>
    {
        if (validationError is null) validationError = a.Message;
    };

    using var sr = new StringReader(xml);
    using var xr = XmlReader.Create(sr, settings);
    try
    {
        while (xr.Read()) { /* advance to trigger validation */ }
    }
    catch (XmlException xe)
    {
        validationError ??= xe.Message;
    }

    error = validationError;
    return error is null;
}

// ---------- main ----------
if (args.Length == 0) { Show(); return; }

var brokerAddr = Arg(args, "--broker", "http://127.0.0.1:7001");
var subject = Arg(args, "--subject", "demo.test");
var inboxDir = Arg(args, "--inbox", Path.Combine(AppContext.BaseDirectory, "data", "inbox_grpc"));
var saveXml = Has(args, "--save-xml");
var xsdPath = Arg(args, "--xsd", "");

Directory.CreateDirectory(inboxDir);
var jsonlPath = Path.Combine(inboxDir, "inbox.jsonl");

// allow HTTP/2 without TLS (dev)
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

using var ch = GrpcChannel.ForAddress(brokerAddr);
var client = new Broker.BrokerClient(ch);

using var call = client.Subscribe(new SubscribeRequest { Subject = subject });
Console.WriteLine($"[gRPC Receiver] subscribed to '{subject}' @ {brokerAddr}");
Console.WriteLine($"[gRPC Receiver] inbox folder: {inboxDir}");
Console.WriteLine($"[gRPC Receiver] saving JSONL to: {jsonlPath}");
if (saveXml) Console.WriteLine($"[gRPC Receiver] XML saving enabled{(string.IsNullOrWhiteSpace(xsdPath) ? "" : $" + XSD validate: {xsdPath}")}");

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

        if (saveXml)
        {
            var xml = EnvelopeToXml(env);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{(env.Type ?? "Type")}_{(env.Subject ?? "Subject")}.xml";
            var xmlPath = Path.Combine(inboxDir, fileName);
            await File.WriteAllTextAsync(xmlPath, xml, Encoding.UTF8);
            Console.WriteLine($"[gRPC Receiver] wrote XML: {xmlPath}");

            if (!string.IsNullOrWhiteSpace(xsdPath) && File.Exists(xsdPath))
            {
                if (TryValidate(xml, xsdPath, out var err))
                    Console.WriteLine("[gRPC Receiver] XML valid (XSD).");
                else
                    Console.WriteLine($"[gRPC Receiver] XML INVALID: {err}");
            }
        }
    }
}
catch (TaskCanceledException) { /* normal on shutdown */ }

using Grpc.Core;
using Proto;
using System.Collections.Concurrent;

namespace BrokerGrpc;

public sealed class BrokerService : Broker.BrokerBase
{
    // Minimal subject -> subscribers (no cleanup/history)
    private readonly ConcurrentDictionary<string, ConcurrentBag<IServerStreamWriter<Envelope>>> _subs = new();

    public override Task<Ack> Publish(PublishRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Type))
            return Task.FromResult(new Ack { Ok = false, Error = "Type required" });
        if (string.IsNullOrWhiteSpace(request.Subject))
            return Task.FromResult(new Ack { Ok = false, Error = "Subject required" });

        var env = new Envelope
        {
            Type = request.Type,
            Subject = request.Subject,
            Payload = request.Payload ?? "",
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString() : request.Id,
            TimestampUnix = request.TimestampUnix != 0
                ? request.TimestampUnix
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        if (_subs.TryGetValue(request.Subject, out var bag))
        {
            foreach (var writer in bag)
            {
                _ = Task.Run(async () =>
                {
                    try { await writer.WriteAsync(env); } catch { /* ignore for the moment */ }
                }, context.CancellationToken);
            }
        }

        return Task.FromResult(new Ack { Ok = true });
    }

    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<Envelope> responseStream, ServerCallContext context)
    {
        var bag = _subs.GetOrAdd(request.Subject, _ => new ConcurrentBag<IServerStreamWriter<Envelope>>());
        bag.Add(responseStream);

        try
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (TaskCanceledException)
        {
            // normal on client disconnect; no removal for the moment
        }
    }
}

using Grpc.Core;
using Proto;
using System.Collections.Concurrent;

namespace BrokerGrpc;

public sealed class BrokerService : Broker.BrokerBase
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<IServerStreamWriter<Envelope>, byte>> _subs = new();
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<Envelope>> _history = new();
    private const int MaxHistory = 100;

    private readonly BrokerState _state;

    public BrokerService(BrokerState state)
    {
        _state = state;
    }

    public override Task<Ack> Publish(PublishRequest request, ServerCallContext context)
    {
        var subject = request.Subject ?? "";

        var subCount = _state.Subs.TryGetValue(subject, out var writers) ? writers.Count : 0;
        Console.WriteLine($"[Broker] Publish {request.Type} '{subject}' -> subscribers={subCount}, payloadLen={request.Payload?.Length ?? 0}");

        if (string.IsNullOrWhiteSpace(request.Type))
            return Task.FromResult(new Ack { Ok = false, Error = "Type required" });
        if (string.IsNullOrWhiteSpace(request.Subject))
            return Task.FromResult(new Ack { Ok = false, Error = "Subject required" });

        var env = new Envelope
        {
            Type = request.Type,
            Subject = subject,
            Payload = request.Payload ?? "",
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString() : request.Id,
            TimestampUnix = request.TimestampUnix != 0
                ? request.TimestampUnix
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // history
        var q = _state.History.GetOrAdd(subject, _ => new ConcurrentQueue<Envelope>());
        q.Enqueue(env);
        while (q.Count > MaxHistory && q.TryDequeue(out _)) { }

        // fan-out
        if (subCount > 0 && writers is not null)
        {
            foreach (var w in writers.Keys)
            {
                _ = Task.Run(async () =>
                {
                    try { await w.WriteAsync(env); }
                    catch { _ = _state.Subs.TryGetValue(subject, out var set) && set.TryRemove(w, out _); }
                }, context.CancellationToken);
            }
        }

        return Task.FromResult(new Ack { Ok = true });
    }

    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<Envelope> responseStream, ServerCallContext context)
    {
        var subject = request.Subject ?? "";
        var set = _state.Subs.GetOrAdd(subject, _ => new ConcurrentDictionary<IServerStreamWriter<Envelope>, byte>());
        set.TryAdd(responseStream, 0);
        Console.WriteLine($"[Broker] Subscribe '{subject}' -> total={set.Count}");

        // replay
        if (_state.History.TryGetValue(subject, out var q))
        {
            foreach (var e in q)
                await responseStream.WriteAsync(e);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (TaskCanceledException) { }
        finally
        {
            set.TryRemove(responseStream, out _);
            Console.WriteLine($"[Broker] Unsubscribe '{subject}' -> total={set.Count}");
        }
    }
}

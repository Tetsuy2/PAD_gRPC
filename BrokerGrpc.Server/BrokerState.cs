using Grpc.Core;
using Proto;
using System.Collections.Concurrent;

namespace BrokerGrpc;

public sealed class BrokerState
{
    // subject -> set of active writers
    public ConcurrentDictionary<string, ConcurrentDictionary<IServerStreamWriter<Envelope>, byte>> Subs { get; } = new();

    // small history per subject for replay-on-subscribe
    public ConcurrentDictionary<string, ConcurrentQueue<Envelope>> History { get; } = new();
}

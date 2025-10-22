using Broker.Services.Interfaces;
using Grpc.Core;
using GrpcAgent;

namespace Broker.Services
{
    public class SenderWorker : IHostedService
    {
        private Timer? _timer;
        private int _busy = 0;

        private readonly IMessageStorageService _messages;
        private readonly IConnectionStorageService _connections;
        private readonly IRouterService _router;
        private readonly int _periodMs;
        private readonly int _maxRetries;

        public SenderWorker(IServiceProvider sp, IConfiguration cfg)
        {
            using var scope = sp.CreateScope();
            _messages = scope.ServiceProvider.GetRequiredService<IMessageStorageService>();
            _connections = scope.ServiceProvider.GetRequiredService<IConnectionStorageService>();
            _router = scope.ServiceProvider.GetRequiredService<IRouterService>();

            var b = cfg.GetSection("Broker");
            _periodMs = Math.Max(200, int.TryParse(b["WorkerPeriodMs"], out var v) ? v : 1000);
            _maxRetries = Math.Max(0, int.TryParse(b["MaxRetries"], out var r) ? r : 3);
        }

        public Task StartAsync(CancellationToken _) { _timer = new Timer(DoWork, null, 0, _periodMs); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken _) { _timer?.Change(Timeout.Infinite, 0); return Task.CompletedTask; }

        private void DoWork(object? _)
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                while (!_messages.IsEmpty())
                {
                    var msg = _messages.GetNext();
                    if (msg is null) break;

                    var targets = _router.ResolveTargets(msg.Topic, msg.Mode);
                    if (targets.Count == 0)
                    {
                        // No subscribers now; push to DLQ for inspection
                        _messages.DeadLetter(msg, "No subscribers");
                        continue;
                    }

                    var delivered = false;
                    var attempts = 0;

                    foreach (var target in targets)
                    {
                        try
                        {
                            if (target.Channel == null || !target.IsAlive()) throw new Exception("Channel not alive");
                            var client = new Notifier.NotifierClient(target.Channel);
                            var reply = client.Notify(new NotifyRequest { Content = msg.Content }, deadline: DateTime.UtcNow.AddSeconds(3));
                            delivered |= reply.IsSuccess;
                            Console.WriteLine($"[BROKER] Notify {target.Address} -> {reply.IsSuccess}");
                        }
                        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded or StatusCode.Cancelled)
                        {
                            _connections.Remove(target);
                            Console.WriteLine($"[BROKER] Removed {target.Address} ({ex.StatusCode}).");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BROKER] Error notifying {target.Address}: {ex.Message}");
                        }

                        attempts++;
                    }

                    msg.Deliveries += attempts;

                    if (!delivered)
                    {
                        if (msg.Deliveries <= _maxRetries)
                        {
                            // retry later: simple backoff by re-enqueueing
                            Task.Delay(250).Wait();
                            _messages.Add(msg);
                        }
                        else
                        {
                            _messages.DeadLetter(msg, "Max retries exceeded");
                        }
                    }
                }
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }
    }
}

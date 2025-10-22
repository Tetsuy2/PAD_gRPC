using System.Collections.Concurrent;
using System.Text.Json;
using Broker.Models;
using Broker.Services.Interfaces;

namespace Broker.Services
{
    public class MessageStorageService : IMessageStorageService
    {
        private readonly ConcurrentQueue<Message> _queue = new();
        private readonly ConcurrentQueue<(Message, string)> _dlq = new();
        private readonly bool _persistent;
        private readonly string _dataDir;
        private readonly string _queueFile;
        private readonly string _dlqFile;

        public MessageStorageService(IConfiguration config)
        {
            var cfg = config.GetSection("Broker");
            _persistent = string.Equals(cfg["StorageMode"], "Persistent", StringComparison.OrdinalIgnoreCase);
            _dataDir = Path.Combine(AppContext.BaseDirectory, cfg["DataDir"] ?? "data");
            Directory.CreateDirectory(_dataDir);
            _queueFile = Path.Combine(_dataDir, "broker_messages.jsonl");
            _dlqFile = Path.Combine(_dataDir, "dead_letters.jsonl");

            if (_persistent && File.Exists(_queueFile))
            {
                foreach (var line in File.ReadAllLines(_queueFile))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var m = JsonSerializer.Deserialize<Message>(line);
                    if (m != null) _queue.Enqueue(m);
                }
            }
        }

        public void Add(Message message)
        {
            _queue.Enqueue(message);
            if (_persistent) Append(_queueFile, message);
        }

        public Message? GetNext()
        {
            _queue.TryDequeue(out var m);
            return m;
        }

        public bool IsEmpty() => _queue.IsEmpty;

        public void DeadLetter(Message message, string reason)
        {
            _dlq.Enqueue((message, reason));
            if (_persistent) Append(_dlqFile, new { message, reason });
        }

        private static void Append(string path, object obj)
        {
            var line = JsonSerializer.Serialize(obj);
            File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}

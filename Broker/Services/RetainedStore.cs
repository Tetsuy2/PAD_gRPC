using System.Collections.Concurrent;
using Broker.Models;
using Broker.Services.Interfaces;

namespace Broker.Services
{
    public class RetainedStore : IRetainedStore
    {
        private readonly ConcurrentDictionary<string, Message> _last = new(StringComparer.Ordinal);

        public void Set(string topic, Message message) => _last[topic] = message;

        public bool TryGet(string topic, out Message? message)
        {
            var ok = _last.TryGetValue(topic, out var m);
            message = m;
            return ok;
        }
    }
}

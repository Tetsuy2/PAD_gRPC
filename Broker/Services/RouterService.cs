using Broker.Models;
using Broker.Services.Interfaces;
using System.Collections.Concurrent;

namespace Broker.Services
{
    public class RouterService : IRouterService
    {
        private readonly IConnectionStorageService _storage;
        private readonly ConcurrentDictionary<string, int> _rr = new();

        public RouterService(IConnectionStorageService storage) => _storage = storage;

        public IList<Connection> ResolveTargets(string topic, string mode)
        {
            var all = _storage.GetConnectionsByTopic(topic);
            if (all.Count == 0) return new List<Connection>();

            if (string.Equals(mode, "UNICAST", StringComparison.OrdinalIgnoreCase))
            {
                var idx = _rr.AddOrUpdate(topic, 0, (_, old) => (old + 1) % all.Count);
                return new List<Connection> { all[idx] };
            }
            return all; // MULTICAST
        }
    }
}

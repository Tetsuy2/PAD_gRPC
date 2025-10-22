using Broker.Models;
using Broker.Services.Interfaces;

namespace Broker.Services
{
    public class ConnectionStorageService : IConnectionStorageService
    {
        private readonly List<Connection> _connections = new();
        private readonly object _locker = new();

        public void Add(Connection connection)
        {
            lock (_locker)
            {
                if (!_connections.Any(c => c.Address == connection.Address && c.Topic == connection.Topic))
                    _connections.Add(connection);
            }
        }

        public IList<Connection> GetConnectionsByTopic(string topic)
        {
            lock (_locker)
            {
                return _connections.Where(c => c.Topic == topic).ToList();
            }
        }

        public void Remove(Connection connection)
        {
            lock (_locker)
            {
                _connections.RemoveAll(c => c.Address == connection.Address && c.Topic == connection.Topic);
            }
        }
    }
}

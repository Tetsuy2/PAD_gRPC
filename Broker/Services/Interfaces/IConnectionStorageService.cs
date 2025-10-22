using Broker.Models;

namespace Broker.Services.Interfaces
{
    public interface IConnectionStorageService
    {
        void Add(Connection connection);
        void Remove(Connection connection);
        IList<Connection> GetConnectionsByTopic(string topic);
    }
}

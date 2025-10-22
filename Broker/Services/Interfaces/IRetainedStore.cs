namespace Broker.Services.Interfaces
{
    using Broker.Models;

    public interface IRetainedStore
    {
        void Set(string topic, Message message);
        bool TryGet(string topic, out Message? message);
    }
}

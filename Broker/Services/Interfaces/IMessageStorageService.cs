using Broker.Models;

namespace Broker.Services.Interfaces
{
    public interface IMessageStorageService
    {
        void Add(Message message);
        Message? GetNext();
        bool IsEmpty();

        // DLQ (for inspection)
        void DeadLetter(Message message, string reason);
    }
}

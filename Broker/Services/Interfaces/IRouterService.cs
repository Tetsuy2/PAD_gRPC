using Broker.Models;

namespace Broker.Services.Interfaces
{
    public interface IRouterService
    {
        // mode: "UNICAST" | "MULTICAST"
        IList<Connection> ResolveTargets(string topic, string mode);
    }
}

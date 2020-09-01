using System;
using System.Threading.Tasks;
using NServiceBus.Extensibility;

namespace NServiceBus.Transport.InMemory
{
    public class SubscriptionManager : IManageSubscriptions
    {
        public SubscriptionManager(EndpointInfo endpoint, InMemoryDatabase inMemoryDatabase)
        {
            Endpoint = endpoint;
            InMemoryDatabase = inMemoryDatabase;
        }

        private EndpointInfo Endpoint { get; }

        private InMemoryDatabase InMemoryDatabase { get; }

        public Task Subscribe(Type eventType, ContextBag context)
        {
            InMemoryDatabase.Subscribe(eventType.AssemblyQualifiedName, Endpoint.Name);

            return Task.CompletedTask;
        }

        public Task Unsubscribe(Type eventType, ContextBag context)
        {
            InMemoryDatabase.Unsubscribe(eventType.AssemblyQualifiedName, Endpoint.Name);

            return Task.CompletedTask;
        }
    }
}

using System.Threading.Tasks;
using NServiceBus.Logging;

namespace NServiceBus.Transport.InMemory
{
    public class InMemoryQueueCreator : ICreateQueues
    {
        private readonly ILog log = LogManager.GetLogger<InMemoryTransport>();

        public InMemoryQueueCreator(InMemoryDatabase inMemoryDatabase)
        {
            InMemoryDatabase = inMemoryDatabase;
        }

        private InMemoryDatabase InMemoryDatabase { get; }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            var sendingAddresses = string.Join("\n", queueBindings.SendingAddresses);
            var receivingAddresses = string.Join("\n", queueBindings.ReceivingAddresses);
            log.Info("Creating queues...");
            log.Info($"Sending addresses: {sendingAddresses}");
            log.Info($"Receiving addresses: {receivingAddresses}");

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                InMemoryDatabase.CreateQueueIfNecessary(sendingAddress, new NsbQueue());
            }

            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                InMemoryDatabase.CreateQueueIfNecessary(receivingAddress, new NsbQueue());
            }

            return Task.CompletedTask;
        }
    }
}

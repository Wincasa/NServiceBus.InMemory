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
            log.Info("Creating queues...");
            var sendingAddresses = string.Join("\n", queueBindings.SendingAddresses);
            var receivingAddresses = string.Join("\n", queueBindings.ReceivingAddresses);
            log.Info($"Sending addresses: {sendingAddresses}");
            log.Info($"Receiving addresses: {receivingAddresses}");

            foreach (var sendingAddress in queueBindings.SendingAddresses)
            {
                InMemoryDatabase.CreateQueueIfNecessary(sendingAddress);
            }

            foreach (var receivingAddress in queueBindings.ReceivingAddresses)
            {
                InMemoryDatabase.CreateQueueIfNecessary(receivingAddress);
            }

            return Task.CompletedTask;
        }
    }
}

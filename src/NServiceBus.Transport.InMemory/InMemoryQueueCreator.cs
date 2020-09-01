using System.Threading.Tasks;
using NServiceBus.Logging;

namespace NServiceBus.Transport.InMemory
{
    public class InMemoryQueueCreator : ICreateQueues
    {
        public InMemoryQueueCreator(InMemoryDatabase inMemoryDatabase)
        {
            InMemoryDatabase = inMemoryDatabase;
        }

        private InMemoryDatabase InMemoryDatabase { get; }

        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
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

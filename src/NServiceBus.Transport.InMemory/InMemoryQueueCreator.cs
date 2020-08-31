using System;
using System.Linq;
using System.Threading.Tasks;

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
            var sendingAddresses = string.Join("\n", queueBindings.SendingAddresses);
            var receivingAddresses = string.Join("\n", queueBindings.ReceivingAddresses);
            Console.WriteLine("Creating queues...");
            Console.WriteLine($"Sending addresses: {sendingAddresses}");
            Console.WriteLine($"Receiving addresses: {receivingAddresses}");

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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NServiceBus.Logging;

namespace NServiceBus.Transport.InMemory
{
    /// <summary>
    /// The in memory database for nsb.
    /// </summary>
    public class InMemoryDatabase : MarshalByRefObject
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _topics = new ConcurrentDictionary<string, HashSet<string>>();
        private readonly ConcurrentDictionary<string, NsbQueue> _queues = new ConcurrentDictionary<string, NsbQueue>();
        private readonly ILog _log = LogManager.GetLogger<InMemoryTransport>();

        private void Send(OutgoingMessage message, string destination)
        {
            try
            {
                CreateQueueIfNecessary(destination);
                _queues[destination].AddMessage(message.Serialize());
            }
            catch (Exception error)
            {
                _log.Error("Failed to send message.", error);
            }
        }

        public override string ToString()
        {
            return $"Queues: {_queues.Count}, Topics: {_topics.Count}";
        }

        /// <summary>
        /// Gets a queue if one exists else returns null.
        /// </summary>
        public NsbQueue GetQueue(string queueName)
        {
            NsbQueue queue;
            return _queues.TryGetValue(queueName, out queue) ? queue : null;
        }

        /// <summary>
        /// Abstraction of the capability to create queues
        /// </summary>
        public bool CreateQueueIfNecessary(string queueName)
        {
            return _queues.TryAdd(queueName, new NsbQueue());
        }

        /// <summary>
        /// Publishes the given messages to all known subscribers
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageType">The type of the message.</param>
        public void Publish(OutgoingMessage message, Type messageType)
        {
            if (_topics.TryGetValue(messageType.AssemblyQualifiedName, out var endpoints))
            {
                foreach (var endpoint in endpoints)
                {
                    if (_queues.TryGetValue(endpoint, out var eventQueue))
                    {
                        eventQueue.AddMessage(message.Serialize());
                    }
                    else
                    {
                        throw new InvalidProgramException("Unable to add event message to the queue.");
                    }
                }
            }
            else
            {
                _log.Warn($"Unable to publish message '{messageType}' because no endpoint subscribed to the message.");
            }
        }

        public void Send(UnicastTransportOperation transportOperation)
        {
            Send(transportOperation.Message, transportOperation.Destination);
        }

        /// <summary>
        /// Subscribes to the given event.
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <param name="endpointName">The endpoint name</param>
        public void Subscribe(string eventType, string endpointName)
        {
            if (!_topics.TryAdd(eventType, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                endpointName
            }))
            {
                _topics[eventType].Add(endpointName);
            }
        }

        /// <summary>
        /// Unsubscribes from the given event.
        /// </summary>
        /// <param name="eventType">The event type</param>
        /// <param name="endpointName">The endpoint name</param>
        public void Unsubscribe(string eventType, string endpointName)
        {
            HashSet<string> endpoints;
            if (_topics.TryGetValue(eventType, out endpoints))
            {
                endpoints.Remove(endpointName);
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Extensibility;
using NServiceBus.Logging;

namespace NServiceBus.Transport.InMemory
{
    public class InMemoryMessagePump : IPushMessages
    {
        private readonly ILog _logger = LogManager.GetLogger<InMemoryMessagePump>();

        private NsbQueue _queue;
        private bool _purgeOnStartup;
        private SemaphoreSlim _concurrencyLimiter;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _processMessagesTask;
        private ConcurrentDictionary<Task, Task> _runningReceiveTasks;
        private Func<MessageContext, Task> _onMessage;
        private Func<ErrorContext, Task<ErrorHandleResult>> _onError;
        private CriticalError _criticalError;

        public InMemoryMessagePump(InMemoryDatabase inMemoryDatabase)
        {
            InMemoryDatabase = inMemoryDatabase;
        }

        private InMemoryDatabase InMemoryDatabase { get; }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            InMemoryDatabase.CreateQueueIfNecessary(settings.InputQueue);
            _queue = InMemoryDatabase.GetQueue(settings.InputQueue);
            if (_queue == null)
            {
                throw new InvalidProgramException($"Unable to get or add the queue '{settings.InputQueue}'.");
            }

            _purgeOnStartup = settings.PurgeOnStartup;
            this._onMessage = onMessage;
            this._onError = onError;
            this._criticalError = criticalError;

            return Task.CompletedTask;
        }

        public void Start(PushRuntimeSettings limitations)
        {
            _runningReceiveTasks = new ConcurrentDictionary<Task, Task>();
            _concurrencyLimiter = new SemaphoreSlim(limitations.MaxConcurrency);
            _cancellationTokenSource = new CancellationTokenSource();

            if (_purgeOnStartup)
            {
                _queue.Clear();
            }

            _processMessagesTask = Task.Factory
                .StartNew(
                    function: ProcessMessages, 
                    cancellationToken: CancellationToken.None,
                    creationOptions: TaskCreationOptions.LongRunning, 
                    scheduler: TaskScheduler.Default)
                .Unwrap();
        }

        public async Task Stop()
        {
            _cancellationTokenSource.Cancel();

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);
            var allTasks = _runningReceiveTasks.Values.Append(_processMessagesTask);
            var finishedTask = await Task.WhenAny(
                Task.WhenAll(allTasks),
                timeoutTask).ConfigureAwait(false);

            if (finishedTask.Equals(timeoutTask))
            {
                _logger.Error("The message pump failed to stop with in the time allowed(30s)");
            }

            _concurrencyLimiter.Dispose();
            _runningReceiveTasks.Clear();
        }

        private async Task ProcessMessages()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await InnerProcessMessages()
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error("File Message pump failed", ex);
                }
            }
        }

        private async Task InnerProcessMessages()
        {
            while (_queue.TryDequeue(out var message))
            {
                await ProcessMessage(message).ConfigureAwait(false);
            }
        }

        private async Task ProcessMessage(SerializedMessage message)
        {
            await _concurrencyLimiter.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);

            var task = Task.Run(async () =>
            {
                try
                {
                    await ProcessMessageWithTransaction(message).ConfigureAwait(false);
                }
                finally
                {
                    _concurrencyLimiter.Release();
                }
            }, _cancellationTokenSource.Token);

            _ = task.ContinueWith(t =>
            {
                _runningReceiveTasks.TryRemove(t, out _);
            }, TaskContinuationOptions.ExecuteSynchronously);

            _ = _runningReceiveTasks.AddOrUpdate(task, task, (k, v) => task);
        }

        private async Task ProcessMessageWithTransaction(SerializedMessage message)
        {
            var transportTransaction = new TransportTransaction();

            var succeeded = await HandleMessageWithRetries(message, transportTransaction, 1).ConfigureAwait(false);

            if (!succeeded)
            {
                _queue.AddMessage(message);
            }
        }

        private async Task<bool> HandleMessageWithRetries(SerializedMessage message, TransportTransaction transportTransaction, int processingAttempt)
        {
            try
            {
                var receiveCancellationTokenSource = new CancellationTokenSource();
                var (messageId, headers, body) = message.Deserialize();

                var pushContext = new MessageContext(messageId, headers, body, transportTransaction, receiveCancellationTokenSource, new ContextBag());

                await _onMessage(pushContext).ConfigureAwait(false);

                return !receiveCancellationTokenSource.IsCancellationRequested;
            }
            catch (Exception e)
            {
                var (messageId, headers, body) = message.Deserialize();
                var errorContext = new ErrorContext(e, headers, messageId, body, transportTransaction, processingAttempt);

                processingAttempt++;

                try
                {
                    var errorHandlingResult = await _onError(errorContext).ConfigureAwait(false);

                    if (errorHandlingResult == ErrorHandleResult.RetryRequired)
                    {
                        return await HandleMessageWithRetries(message, transportTransaction, processingAttempt).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    _criticalError.Raise($"Failed to execute recoverability policy for message with native ID: `{messageId}`", exception);

                    return await HandleMessageWithRetries(message, transportTransaction, processingAttempt).ConfigureAwait(false);
                }

                return true;
            }
        }
    }
}

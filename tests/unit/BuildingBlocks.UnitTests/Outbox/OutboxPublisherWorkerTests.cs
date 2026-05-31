using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace Urfu.Link.BuildingBlocks.UnitTests.Outbox;

public sealed class OutboxPublisherWorkerTests
{
    [Fact]
    public async Task Recovers_after_transient_recovery_failure_instead_of_faulting_host()
    {
        var store = new ThrowOnceOutboxStore(
            failOnRecover: true,
            secondRecoverObserved: new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        using var worker = CreateWorker(store);

        await worker.StartAsync(CancellationToken.None);
        await store.SecondRecoverObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        store.RecoverCalls.Should().BeGreaterThanOrEqualTo(2);
        worker.ExecuteTask.Should().NotBeNull();
        worker.ExecuteTask!.IsFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task Continues_after_transient_dequeue_failure_instead_of_faulting_host()
    {
        var store = new ThrowOnceOutboxStore(
            failOnDequeue: true,
            secondDequeueObserved: new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        using var worker = CreateWorker(store);

        await worker.StartAsync(CancellationToken.None);
        await store.SecondDequeueObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await worker.StopAsync(CancellationToken.None);

        store.DequeueCalls.Should().BeGreaterThanOrEqualTo(2);
        worker.ExecuteTask.Should().NotBeNull();
        worker.ExecuteTask!.IsFaulted.Should().BeFalse();
    }

    private static OutboxPublisherWorker CreateWorker(IOutboxStore store)
    {
        return new OutboxPublisherWorker(
            store,
            new NoopKafkaPublisher(),
            Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromMilliseconds(10) }),
            NullLogger<OutboxPublisherWorker>.Instance);
    }

    private sealed class ThrowOnceOutboxStore(
        bool failOnRecover = false,
        bool failOnDequeue = false,
        TaskCompletionSource? secondRecoverObserved = null,
        TaskCompletionSource? secondDequeueObserved = null) : IOutboxStore
    {
        public TaskCompletionSource SecondRecoverObserved { get; } =
            secondRecoverObserved ?? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondDequeueObserved { get; } =
            secondDequeueObserved ?? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RecoverCalls { get; private set; }

        public int DequeueCalls { get; private set; }

        public ValueTask EnqueueAsync<TEvent>(
            string topic,
            IntegrationEnvelope<TEvent> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            return ValueTask.CompletedTask;
        }

        public Task RecoverAsync(CancellationToken cancellationToken = default)
        {
            RecoverCalls++;
            if (failOnRecover && RecoverCalls == 1)
            {
                throw CreateRedisFailure();
            }

            if (RecoverCalls >= 2)
            {
                SecondRecoverObserved.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public Task<OutboxMessage?> DequeueAsync(CancellationToken cancellationToken = default)
        {
            DequeueCalls++;
            if (failOnDequeue && DequeueCalls == 1)
            {
                throw CreateRedisFailure();
            }

            if (DequeueCalls >= 2)
            {
                SecondDequeueObserved.TrySetResult();
            }

            return Task.FromResult<OutboxMessage?>(null);
        }

        public Task CompleteAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        private static RedisConnectionException CreateRedisFailure()
        {
            return new RedisConnectionException(ConnectionFailureType.SocketFailure, "transient redis failure");
        }
    }

    private sealed class NoopKafkaPublisher : IKafkaPublisher
    {
        public Task PublishAsync<TEvent>(
            string topic,
            IntegrationEnvelope<TEvent> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            return Task.CompletedTask;
        }

        public Task PublishSerializedAsync(string topic, string key, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

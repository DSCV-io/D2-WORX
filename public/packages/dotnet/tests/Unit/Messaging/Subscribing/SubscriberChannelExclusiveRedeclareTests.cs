// -----------------------------------------------------------------------
// <copyright file="SubscriberChannelExclusiveRedeclareTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.Subscribing;

using AwesomeAssertions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Messaging.RabbitMq.Subscribing;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Result;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Unit pins for FanoutExclusiveAutoDelete re-declare on the consumer channel
/// before BasicConsume, and for orphan-channel dispose when StartAsync fails
/// after channel assignment.
/// </summary>
/// <remarks>
/// <para>
/// Fail-without-fix (re-declare): remove the
/// <c>FanoutExclusiveAutoDelete</c> branch in <see cref="SubscriberChannel.StartAsync"/>
/// and the primary exclusive <c>QueueDeclare</c> call disappears before
/// <c>BasicConsume</c> — this test asserts that ordering + exclusive flags.
/// Fail-without-fix (orphan dispose): remove the catch dispose and
/// <see cref="RecordingChannel.DisposeAsyncCallCount"/> stays 0 after a QoS throw.
/// </para>
/// <para>
/// Deliberately does NOT call <see cref="MessageWireResolver.ClearCache"/>.
/// Fixture types here are unique and only need <c>RegisterForTesting</c>
/// overwrite. A global clear races parallel Integration tests that seed
/// via <c>IntegrationMessageFixtures</c> (see KeyRotatedEventTests note).
/// </para>
/// </remarks>
public sealed class SubscriberChannelExclusiveRedeclareTests
{
    [Fact]
    public async Task StartAsync_FanoutExclusive_RedeclaresExclusiveQueue_BeforeBasicConsume()
    {
        const string queue = "fanout-exclusive-redeclare-q";
        const string exchange = "d2.test.fanout.exclusive";
        MessageWireResolver.RegisterForTesting(
            typeof(ExclusiveFixtureMessage),
            new MqMessageDescriptor(
                Constant: "ExclusiveFixture",
                MessageTypeName: typeof(ExclusiveFixtureMessage).FullName!,
                Exchange: exchange,
                ExchangeType: "fanout",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "unit fixture",
                DefaultRoutingKey: string.Empty));

        var channel = new RecordingChannel();
        var conn = new SingleChannelConnection(channel);

        var registration = BuildRegistration(
            queue, QueuePattern.FanoutExclusiveAutoDelete, routingKey: string.Empty);

        var registry = new SubscriberRegistry([registration]);
        var sp = new ServiceCollection().BuildServiceProvider();

        var sub = new SubscriberChannel(
            conn,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            registration,
            NullLogger<SubscriberChannel>.Instance);

        await sub.StartAsync(CancellationToken.None);

        var (durable, exclusive, autoDelete) =
            DefaultTopologyDeclarer.QueueFlagsFor(QueuePattern.FanoutExclusiveAutoDelete);

        durable.Should().BeFalse();
        exclusive.Should().BeTrue();
        autoDelete.Should().BeTrue();

        // Main exchange + DLX declared before primary queue (DefaultTopologyDeclarer order).
        var mainExchange = channel.ExchangeDeclares.Should().Contain(
            e => e.Exchange == exchange).Subject;

        mainExchange.Type.Should().NotBeNullOrEmpty();
        mainExchange.Durable.Should().BeTrue("main exchange is durable by DefaultTopologyDeclarer");
        mainExchange.AutoDelete.Should().BeFalse();

        channel.ExchangeDeclares.Should().Contain(
            e => e.Exchange == DlqNaming.DlxFor(queue) && e.Type == ExchangeType.Fanout);

        var primaryDeclare = channel.QueueDeclares.Should().Contain(
            q => q.Queue == queue).Subject;

        primaryDeclare.Durable.Should().Be(durable);
        primaryDeclare.Exclusive.Should().Be(exclusive);
        primaryDeclare.AutoDelete.Should().Be(autoDelete);

        primaryDeclare.Arguments.Should().ContainKey("x-dead-letter-exchange")
            .WhoseValue.Should().Be(DlqNaming.DlxFor(queue));

        channel.QueueDeclares.Should().Contain(q => q.Queue == DlqNaming.DlqFor(queue));

        var primaryIdx = IndexOfPrefix(channel.CallLog, "QueueDeclare:" + queue);
        var consumeIdx = IndexOfPrefix(channel.CallLog, "BasicConsume:" + queue);
        var dlxIdx = IndexOfPrefix(channel.CallLog, "ExchangeDeclare:" + DlqNaming.DlxFor(queue));

        primaryIdx.Should().BeGreaterThanOrEqualTo(0);

        consumeIdx.Should().BeGreaterThan(
            primaryIdx,
            "exclusive queue must be re-declared on the consumer channel before BasicConsume");

        dlxIdx.Should().BeGreaterThanOrEqualTo(0);

        dlxIdx.Should().BeLessThan(
            primaryIdx,
            "DLX must be declared before the primary queue with x-dead-letter-exchange");

        await sub.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_CompetingConsumer_DoesNotRedeclareExclusiveQueue()
    {
        const string queue = "competing-no-redeclare-q";
        MessageWireResolver.RegisterForTesting(
            typeof(ExclusiveFixtureMessage),
            new MqMessageDescriptor(
                Constant: "ExclusiveFixture",
                MessageTypeName: typeof(ExclusiveFixtureMessage).FullName!,
                Exchange: "d2.test.topic",
                ExchangeType: "topic",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "unit fixture",
                DefaultRoutingKey: "#"));

        var channel = new RecordingChannel();
        var conn = new SingleChannelConnection(channel);

        var registration = BuildRegistration(
            queue, QueuePattern.CompetingConsumer, routingKey: "#");

        var registry = new SubscriberRegistry([registration]);
        var sp = new ServiceCollection().BuildServiceProvider();

        var sub = new SubscriberChannel(
            conn,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            registration,
            NullLogger<SubscriberChannel>.Instance);

        await sub.StartAsync(CancellationToken.None);

        channel.QueueDeclares.Should().BeEmpty(
            "only FanoutExclusiveAutoDelete re-declares on the consumer channel");

        channel.CallLog.Should().Contain(
            c => c.StartsWith("BasicConsume:" + queue, StringComparison.Ordinal));

        await sub.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_QosThrowsAfterChannelAssigned_DisposesOrphanChannel()
    {
        var channel = new RecordingChannel { ThrowOnBasicQos = true };
        var conn = new SingleChannelConnection(channel);

        var registration = BuildRegistration(
            "orphan-qos-q", QueuePattern.CompetingConsumer, routingKey: "#");

        MessageWireResolver.RegisterForTesting(
            typeof(ExclusiveFixtureMessage),
            new MqMessageDescriptor(
                Constant: "ExclusiveFixture",
                MessageTypeName: typeof(ExclusiveFixtureMessage).FullName!,
                Exchange: "d2.test.topic",
                ExchangeType: "topic",
                Encryption: MqMessageDescriptor.PLAINTEXT,
                EncryptionReason: "unit fixture",
                DefaultRoutingKey: "#"));

        var registry = new SubscriberRegistry([registration]);
        var sp = new ServiceCollection().BuildServiceProvider();

        var sub = new SubscriberChannel(
            conn,
            sp.GetRequiredService<IServiceScopeFactory>(),
            new HandlerDispatcherFactory(registry),
            registration,
            NullLogger<SubscriberChannel>.Instance);

        var act = async () => await sub.StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("qos-fixture-blow-up");

        channel.DisposeAsyncCallCount.Should().Be(
            1, "orphan channel assigned before the throw must be disposed");

        await sub.DisposeAsync();
    }

    private static int IndexOfPrefix(IReadOnlyList<string> log, string prefix)
    {
        for (var i = 0; i < log.Count; i++)
        {
            if (log[i].StartsWith(prefix, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static SubscriberRegistration BuildRegistration(
        string queueName,
        QueuePattern pattern,
        string routingKey)
    {
        var descriptor = new MqSubscriptionDescriptor(
            Constant: "ExclusiveSub",
            MessageTypeName: typeof(ExclusiveFixtureMessage).FullName!,
            QueueName: queueName,
            Pattern: pattern,
            RoutingKeyBinding: routingKey,
            Prefetch: 1,
            Idempotency: false,
            TieredRetry: null);

        return new SubscriberRegistration(
            HandlerType: typeof(ExclusiveFixtureHandler),
            MessageType: typeof(ExclusiveFixtureMessage),
            Descriptor: descriptor,
            ResolvedQueueName: queueName);
    }

    /// <summary>Fixture message type for wire resolution.</summary>
    public sealed class ExclusiveFixtureMessage
    {
    }

    /// <summary>Companion handler for <see cref="ExclusiveFixtureMessage"/>.</summary>
    public sealed class ExclusiveFixtureHandler
        : BaseHandler<ExclusiveFixtureHandler, ExclusiveFixtureMessage, Unit>
    {
        /// <summary>Initializes the handler.</summary>
        /// <param name="context">DI-resolved handler context.</param>
        public ExclusiveFixtureHandler(HandlerContext<ExclusiveFixtureHandler> context)
            : base(context)
        {
        }

        /// <inheritdoc />
        protected override ValueTask<D2Result<Unit>> ExecuteAsync(
            ExclusiveFixtureMessage input,
            CancellationToken ct)
            => new(D2Result<Unit>.Ok(Unit.Value));
    }

    private sealed class SingleChannelConnection(IChannel channel) : ID2Connection
    {
        public bool IsOpen => true;

        public Task ReadyTask { get; } = Task.CompletedTask;

        public void StartReconnectLoop()
        {
        }

        public ValueTask<IChannel> CreateChannelAsync(
            CreateChannelOptions? options = null,
            CancellationToken ct = default)
            => new(channel);

        public ValueTask DisposeAsync() => default;
    }

    /// <summary>Records topology + consume calls for StartAsync assertions.</summary>
    private sealed class RecordingChannel : IChannel
    {
        private readonly List<string> r_callLog = [];
        private readonly List<QueueDeclareRecord> r_queueDeclares = [];
        private readonly List<ExchangeDeclareRecord> r_exchangeDeclares = [];
        private int _disposeAsyncCount;

#pragma warning disable CS0067 // Interface-required events unused by this recording stub.
        public event AsyncEventHandler<BasicAckEventArgs>? BasicAcksAsync;

        public event AsyncEventHandler<BasicNackEventArgs>? BasicNacksAsync;

        public event AsyncEventHandler<BasicReturnEventArgs>? BasicReturnAsync;

        public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync;

        public event AsyncEventHandler<FlowControlEventArgs>? FlowControlAsync;

        public event AsyncEventHandler<ShutdownEventArgs>? ChannelShutdownAsync;
#pragma warning restore CS0067

        public bool ThrowOnBasicQos { get; init; }

        public IReadOnlyList<string> CallLog
        {
            get
            {
                lock (r_callLog)
                    return r_callLog.ToArray();
            }
        }

        public IReadOnlyList<QueueDeclareRecord> QueueDeclares
        {
            get
            {
                lock (r_queueDeclares)
                    return r_queueDeclares.ToArray();
            }
        }

        public IReadOnlyList<ExchangeDeclareRecord> ExchangeDeclares
        {
            get
            {
                lock (r_exchangeDeclares)
                    return r_exchangeDeclares.ToArray();
            }
        }

        public int DisposeAsyncCallCount => Volatile.Read(ref _disposeAsyncCount);

        public int ChannelNumber => 1;

        public ShutdownEventArgs? CloseReason => null;

        public IAsyncBasicConsumer? DefaultConsumer { get; set; }

        public bool IsClosed => false;

        public bool IsOpen => true;

        public string? CurrentQueue => null;

        public TimeSpan ContinuationTimeout { get; set; } = TimeSpan.FromSeconds(5);

        public Task BasicQosAsync(
            uint prefetchSize,
            ushort prefetchCount,
            bool global,
            CancellationToken cancellationToken = default)
        {
            Record("BasicQos");

            if (ThrowOnBasicQos)
                throw new InvalidOperationException("qos-fixture-blow-up");

            return Task.CompletedTask;
        }

        public Task ExchangeDeclareAsync(
            string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object?>? arguments = null,
            bool passive = false,
            bool noWait = false,
            CancellationToken cancellationToken = default)
        {
            Record("ExchangeDeclare:" + exchange);

            lock (r_exchangeDeclares)
                r_exchangeDeclares.Add(new ExchangeDeclareRecord(exchange, type, durable, autoDelete));

            return Task.CompletedTask;
        }

        public Task<QueueDeclareOk> QueueDeclareAsync(
            string queue,
            bool durable,
            bool exclusive,
            bool autoDelete,
            IDictionary<string, object?>? arguments = null,
            bool passive = false,
            bool noWait = false,
            CancellationToken cancellationToken = default)
        {
            Record("QueueDeclare:" + queue);

            lock (r_queueDeclares)
            {
                r_queueDeclares.Add(
                    new QueueDeclareRecord(
                        queue,
                        durable,
                        exclusive,
                        autoDelete,
                        arguments is null
                            ? new Dictionary<string, object?>(StringComparer.Ordinal)
                            : new Dictionary<string, object?>(arguments, StringComparer.Ordinal)));
            }

            return Task.FromResult(new QueueDeclareOk(queue, 0, 0));
        }

        public Task QueueBindAsync(
            string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object?>? arguments = null,
            bool noWait = false,
            CancellationToken cancellationToken = default)
        {
            Record("QueueBind:" + queue + "->" + exchange);
            return Task.CompletedTask;
        }

        public Task<string> BasicConsumeAsync(
            string queue,
            bool autoAck,
            string consumerTag,
            bool noLocal,
            bool exclusive,
            IDictionary<string, object?>? arguments,
            IAsyncBasicConsumer consumer,
            CancellationToken cancellationToken = default)
        {
            Record("BasicConsume:" + queue);
            return Task.FromResult(consumerTag.Length > 0 ? consumerTag : "ctag-fixture");
        }

        public Task BasicCancelAsync(
            string consumerTag,
            bool noWait = false,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public ValueTask BasicAckAsync(
            ulong deliveryTag,
            bool multiple,
            CancellationToken cancellationToken = default)
            => default;

        public ValueTask BasicNackAsync(
            ulong deliveryTag,
            bool multiple,
            bool requeue,
            CancellationToken cancellationToken = default)
            => default;

        public ValueTask BasicRejectAsync(
            ulong deliveryTag,
            bool requeue,
            CancellationToken cancellationToken = default)
            => default;

        public Task CloseAsync(
            ushort replyCode,
            string replyText,
            bool abort,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CloseAsync(ShutdownEventArgs reason, bool abort) => Task.CompletedTask;

#pragma warning disable CS0618 // obsolete CloseAsync overload still on IChannel
        // Non-optional ct so this overload does not hide the two-arg form
        // (inspectcode: method with optional parameter is hidden by overload).
        public Task CloseAsync(
            ShutdownEventArgs reason,
            bool abort,
            CancellationToken cancellationToken)
            => Task.CompletedTask;
#pragma warning restore CS0618

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeAsyncCount);
            return default;
        }

        public void Dispose()
        {
        }

        public Task<BasicGetResult?> BasicGetAsync(
            string queue,
            bool autoAck,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask BasicPublishAsync<TProperties>(
            string exchange,
            string routingKey,
            bool mandatory,
            TProperties basicProperties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => throw new NotImplementedException();

        public ValueTask BasicPublishAsync<TProperties>(
            CachedString exchange,
            CachedString routingKey,
            bool mandatory,
            TProperties basicProperties,
            ReadOnlyMemory<byte> body,
            CancellationToken cancellationToken = default)
            where TProperties : IReadOnlyBasicProperties, IAmqpHeader
            => throw new NotImplementedException();

        public Task ExchangeDeclarePassiveAsync(
            string exchange,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExchangeDeleteAsync(
            string exchange,
            bool ifUnused = false,
            bool noWait = false,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExchangeBindAsync(
            string destination,
            string source,
            string routingKey,
            IDictionary<string, object?>? arguments = null,
            bool noWait = false,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task ExchangeUnbindAsync(
            string destination,
            string source,
            string routingKey,
            IDictionary<string, object?>? arguments = null,
            bool noWait = false,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<QueueDeclareOk> QueueDeclarePassiveAsync(
            string queue,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<uint> QueueDeleteAsync(
            string queue,
            bool ifUnused,
            bool ifEmpty,
            bool noWait = false,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<uint> QueuePurgeAsync(
            string queue,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task QueueUnbindAsync(
            string queue,
            string exchange,
            string routingKey,
            IDictionary<string, object?>? arguments = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<uint> MessageCountAsync(
            string queue,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<uint> ConsumerCountAsync(
            string queue,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task TxCommitAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task TxRollbackAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task TxSelectAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask<ulong> GetNextPublishSequenceNumberAsync(
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        private void Record(string entry)
        {
            lock (r_callLog)
                r_callLog.Add(entry);
        }
    }

    private sealed record QueueDeclareRecord(
        string Queue,
        bool Durable,
        bool Exclusive,
        bool AutoDelete,
        IReadOnlyDictionary<string, object?> Arguments);

    private sealed record ExchangeDeclareRecord(
        string Exchange,
        string Type,
        bool Durable,
        bool AutoDelete);
}

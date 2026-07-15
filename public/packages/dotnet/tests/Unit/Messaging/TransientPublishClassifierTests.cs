// -----------------------------------------------------------------------
// <copyright file="TransientPublishClassifierTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Runtime.CompilerServices;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Publishing;
using RabbitMQ.Client.Exceptions;
using Xunit;

public sealed class TransientPublishClassifierTests
{
    [Fact]
    public void IsTransient_BrokerUnavailable_True()
    {
        var ex = new BrokerUnavailableException("offline");
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_AlreadyClosed_True()
    {
        // RabbitMQ.Client's AlreadyClosedException takes a ShutdownEventArgs in
        // its public ctor — awkward to construct in unit tests. The classifier
        // only inspects the runtime type, so an uninitialized instance suffices.
        var ex = (AlreadyClosedException)RuntimeHelpers.GetUninitializedObject(
            typeof(AlreadyClosedException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_OperationInterrupted_True()
    {
        var ex = (OperationInterruptedException)RuntimeHelpers.GetUninitializedObject(
            typeof(OperationInterruptedException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_TimeoutException_True()
    {
        TransientPublishClassifier.IsTransient(new TimeoutException()).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_BrokerUnreachable_True()
    {
        // BrokerUnreachableException is in the classifier switch; pin it here
        // so a future "simplify" refactor doesn't quietly drop the case.
        var ex = (BrokerUnreachableException)RuntimeHelpers.GetUninitializedObject(
            typeof(BrokerUnreachableException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }

    [Fact]
    public void IsTransient_InvalidOperation_False()
    {
        // Programmer errors are not transient — surfacing immediately tells
        // the caller their pipeline is broken (e.g. missing [MqPub]).
        TransientPublishClassifier.IsTransient(new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_ArgumentException_False()
    {
        TransientPublishClassifier.IsTransient(new ArgumentException("bad")).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_NullArg_ReturnsFalse()
    {
        // Switch on null matches default arm, then RetryHelper.IsTransientException's
        // own switch matches default → returns false. Defensive: the publisher
        // should never see a null exception, but if it did the contract is
        // "not transient" rather than NRE-cascading from the classifier itself.
        TransientPublishClassifier.IsTransient(null!).Should().BeFalse();
    }

    [Fact]
    public void IsTransient_PublishException_NotReturn_True()
    {
        // M3: broker NACK on a confirm — queue full / mirror sync / restart.
        // PublishException with IsReturn=false IS transient (worth a backoff
        // retry). The exception's IsReturn property is read-only so we use
        // an uninitialized instance — defaults make IsReturn=false.
        var ex = (PublishException)RuntimeHelpers.GetUninitializedObject(
            typeof(PublishException));
        TransientPublishClassifier.IsTransient(ex).Should().BeTrue();
    }
}

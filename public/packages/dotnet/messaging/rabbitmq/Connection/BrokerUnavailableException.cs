// -----------------------------------------------------------------------
// <copyright file="BrokerUnavailableException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Connection;

/// <summary>
/// Thrown by <see cref="ID2Connection.CreateChannelAsync"/> when the
/// underlying RabbitMQ connection is not currently open. The publisher path
/// catches this and returns <c>D2Result.ServiceUnavailable</c> so callers
/// can degrade gracefully instead of crashing.
/// </summary>
public sealed class BrokerUnavailableException : Exception
{
    /// <summary>Initializes a new instance with the default message.</summary>
    public BrokerUnavailableException()
        : base("RabbitMQ broker connection is not currently open. "
            + "The reconnect loop will keep trying; publish will succeed "
            + "again once the broker is reachable.")
    {
    }

    /// <summary>Initializes a new instance with a custom message.</summary>
    /// <param name="message">Custom message.</param>
    public BrokerUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a custom message + inner.</summary>
    /// <param name="message">Custom message.</param>
    /// <param name="inner">Underlying exception.</param>
    public BrokerUnavailableException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

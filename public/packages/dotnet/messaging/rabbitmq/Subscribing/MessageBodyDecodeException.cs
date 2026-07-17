// -----------------------------------------------------------------------
// <copyright file="MessageBodyDecodeException.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

/// <summary>
/// Thrown by <see cref="IHandlerDispatcher.DispatchAsync"/> when the AMQP
/// body cannot be decoded — bad encryption frame, missing kid, AEAD tag
/// mismatch, malformed JSON. Caller (consumer) catches and routes the
/// message to its DLQ with cause <c>DECRYPT_FAILURE</c> /
/// <c>DESERIALIZE_FAILURE</c> based on the inner exception type.
/// </summary>
internal sealed class MessageBodyDecodeException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="message">Diagnostic message.</param>
    /// <param name="inner">The original decode failure.</param>
    public MessageBodyDecodeException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

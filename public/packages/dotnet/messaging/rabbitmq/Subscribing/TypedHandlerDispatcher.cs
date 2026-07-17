// -----------------------------------------------------------------------
// <copyright file="TypedHandlerDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Closed-over-generics <see cref="IHandlerDispatcher"/>. Knows the
/// concrete <typeparamref name="TSubscriber"/> handler + <typeparamref name="TMessage"/>
/// at compile time so per-message dispatch costs zero reflection.
/// </summary>
/// <typeparam name="TSubscriber">
/// The subscriber handler type, registered in DI as Transient. Constrained
/// to <see cref="BaseHandler{TSelf, TInput, TOutput}"/> so we can call
/// <see cref="BaseHandler{TSelf, TInput, TOutput}.HandleAsync"/> directly.
/// </typeparam>
/// <typeparam name="TMessage">The message payload type.</typeparam>
/// <remarks>
/// Per-message <c>IRequestContext</c> is NOT populated from the wire — there
/// is no envelope on the wire. Cross-hop trace correlation rides in the
/// W3C <c>traceparent</c> AMQP header (read by the consumer span); any
/// caller-identity / org / scope fields the handler needs come from the
/// typed message body itself.
/// </remarks>
internal sealed class TypedHandlerDispatcher<TSubscriber, TMessage> : IHandlerDispatcher
    where TSubscriber : BaseHandler<TSubscriber, TMessage, Unit>
    where TMessage : class
{
    /// <inheritdoc />
    public async ValueTask<D2Result> DispatchAsync(
        IServiceProvider scope,
        ReadOnlyMemory<byte> body,
        CancellationToken ct)
    {
        TMessage message;
        try
        {
            // Resolve the wire descriptor + decode in one shot. Same throw
            // semantics as the publisher: missing [MqPub] / unknown constant
            // surfaces as InvalidOperationException, which the dispatcher
            // wraps below as a decode failure (DLQ-bound). The descriptor
            // tells the composer whether to AEAD-decrypt or treat as plaintext.
            var descriptor = MessageWireResolver.Resolve(typeof(TMessage));
            message = EncryptedBodyComposer.Decompose<TMessage>(body.Span, descriptor, scope);
        }
        catch (Exception ex)
        {
            // Surface as a typed boundary failure; caller maps to DLQ via
            // DlqFailureHeaderBuilder. Don't wrap in D2Result.UnhandledException
            // — the consumer treats decode failures specially (different DLQ
            // cause).
            throw new MessageBodyDecodeException(
                "Failed to decode message body. See inner exception for cause.", ex);
        }

        var handler = scope.GetRequiredService<TSubscriber>();
        var result = await handler.HandleAsync(message, ct);
        return result;
    }
}

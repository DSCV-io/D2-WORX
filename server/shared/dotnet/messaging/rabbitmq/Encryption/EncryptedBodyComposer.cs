// -----------------------------------------------------------------------
// <copyright file="EncryptedBodyComposer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.RabbitMq.Encryption;

using System.Text;
using System.Text.Json;
using D2.Shared.Encryption;
using D2.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Composes the AMQP body bytes for a publish (JSON-serialize the message,
/// optionally encrypt) and decomposes them on consume (optionally decrypt,
/// JSON-parse).
/// </summary>
/// <remarks>
/// <para>
/// The wire body is JUST the serialized message — no envelope wrapper. For
/// types whose <see cref="MqMessageDescriptor.Encryption"/> is a registered
/// domain, the message JSON is AEAD-encrypted into a frame; for
/// <see cref="MqMessageDescriptor.IsPlaintext">plaintext</see> types the JSON
/// ships as-is.
/// </para>
/// <para>
/// Encryption here is about <strong>confidentiality of the message payload</strong>,
/// not about identity propagation. Cross-hop trace correlation rides in the
/// W3C <c>traceparent</c> AMQP header.
/// </para>
/// </remarks>
internal static class EncryptedBodyComposer
{
    /// <summary>
    /// Composes the AMQP body bytes for a publish using a pre-resolved
    /// <see cref="MqMessageDescriptor"/>.
    /// </summary>
    /// <typeparam name="TMessage">Message type.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="descriptor">The message's resolved publisher contract.</param>
    /// <param name="serviceProvider">For keyed <see cref="IPayloadCrypto"/> resolution.</param>
    /// <returns>
    /// A tuple of body bytes + the kid used (null for plaintext).
    /// </returns>
    public static (byte[] Body, string? Kid) Compose<TMessage>(
        TMessage message,
        MqMessageDescriptor descriptor,
        IServiceProvider serviceProvider)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var json = JsonSerializer.SerializeToUtf8Bytes(
            message, MessagingJsonOptions.Options);

        if (descriptor.IsPlaintext)
            return (json, null);

        var crypto = serviceProvider.GetRequiredKeyedService<IPayloadCrypto>(descriptor.Encryption);
        var frame = crypto.Encrypt(json);
        var kid = ReadKidFromFrame(frame);
        return (frame, kid);
    }

    /// <summary>
    /// Decomposes incoming AMQP body bytes into the typed message using a
    /// pre-resolved <see cref="MqMessageDescriptor"/>. Throws on decrypt
    /// failure or JSON parse error — callers map to DLQ.
    /// </summary>
    /// <typeparam name="TMessage">Expected message type.</typeparam>
    /// <param name="body">Incoming body bytes.</param>
    /// <param name="descriptor">The message's resolved publisher contract.</param>
    /// <param name="serviceProvider">For keyed <see cref="IPayloadCrypto"/> resolution.</param>
    /// <returns>The decoded message.</returns>
    public static TMessage Decompose<TMessage>(
        ReadOnlySpan<byte> body,
        MqMessageDescriptor descriptor,
        IServiceProvider serviceProvider)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        byte[] json;
        if (descriptor.IsPlaintext)
        {
            json = body.ToArray();
        }
        else
        {
            var crypto = serviceProvider.GetRequiredKeyedService<IPayloadCrypto>(
                descriptor.Encryption);
            json = crypto.Decrypt(body);
        }

        return JsonSerializer.Deserialize<TMessage>(
            json, MessagingJsonOptions.Options)
            ?? throw new InvalidOperationException(
                "Decoded body deserialized to null — wire format violation.");
    }

    /// <summary>
    /// Extracts the kid from an encryption frame's header bytes (used to
    /// populate the <c>x-d2-encryption-kid</c> AMQP header alongside the
    /// encrypted body).
    /// </summary>
    /// <param name="frame">The encryption frame produced by
    /// <see cref="IPayloadCrypto.Encrypt"/>.</param>
    /// <returns>The kid string parsed from the frame's header.</returns>
    public static string ReadKidFromFrame(ReadOnlySpan<byte> frame)
    {
        // Frame layout: [version=1][kid_len:1][kid:UTF-8][nonce:12][ct+tag]
        if (frame.Length < 2)
        {
            throw new InvalidOperationException(
                "Frame too short to read kid header.");
        }

        // L2: version byte must match the only format we know how to read.
        // A future format bump (different field ordering, longer kid_len
        // prefix, etc.) MUST surface here — silently parsing an unknown
        // frame would emit a garbage `x-d2-encryption-kid` header that
        // ops use for archive-key triage, which is worse than a hard fail.
        if (frame[0] != 1)
        {
            throw new InvalidOperationException(
                $"Unknown encryption frame version: {frame[0]}. Expected 1.");
        }

        var kidLen = frame[1];
        if (frame.Length < 2 + kidLen)
        {
            throw new InvalidOperationException(
                "Frame too short for declared kid length.");
        }

        return Encoding.UTF8.GetString(frame.Slice(2, kidLen));
    }
}

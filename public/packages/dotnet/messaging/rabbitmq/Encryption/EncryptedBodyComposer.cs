// -----------------------------------------------------------------------
// <copyright file="EncryptedBodyComposer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Encryption;

using System.Text;
using System.Text.Json;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
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

        // Sealed (asymmetric) mode: seal to the recipient SERVICE's public key
        // via the keyed IPayloadSealer resolved by the consumer service id. A
        // producer host that never registered a sealer for that service lacks
        // the keyed registration → GetRequiredKeyedService throws → the publish
        // fails loud (no plaintext fallback), which is the correct fail-closed
        // shape for a payload that must never ship unencrypted.
        if (descriptor.IsSealed)
        {
            var sealer = serviceProvider.GetRequiredKeyedService<IPayloadSealer>(
                ConsumerServiceKey(descriptor));
            var sealedFrame = sealer.Seal(json);
            return (sealedFrame, ReadKidFromFrame(sealedFrame));
        }

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
        else if (descriptor.IsSealed)
        {
            // Sealed (asymmetric) mode: open with the recipient SERVICE's
            // private key via the keyed IPayloadOpener resolved by the consumer
            // service id. On the legitimate consumer host that id equals its own
            // service id, so opener-keyed-by-consumer-service IS opener-keyed-by-
            // own-identity; a non-consumer host lacks the registration →
            // GetRequiredKeyedService throws → the caller maps the throw to DLQ
            // (never a silent drop, never plaintext).
            var opener = serviceProvider.GetRequiredKeyedService<IPayloadOpener>(
                ConsumerServiceKey(descriptor));
            json = opener.Open(body);
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
    /// encrypted body). Version-aware: a symmetric version-1 frame yields
    /// its keyring kid; a sealed version-2 frame yields its recipient kid —
    /// both header families share the same
    /// <c>[version:1][kid_len:1][kid:UTF-8]</c> prefix shape, so the read
    /// path is identical and only the layout constants differ. For a sealed
    /// frame the recipient kid is what DLQ triage needs to identify the
    /// archive-opener key without decrypting.
    /// </summary>
    /// <param name="frame">The encryption frame produced by
    /// <see cref="IPayloadCrypto.Encrypt"/> or <see cref="IPayloadSealer.Seal"/>.</param>
    /// <returns>The kid string parsed from the frame's header.</returns>
    public static string ReadKidFromFrame(ReadOnlySpan<byte> frame)
    {
        // v1: [version=1][kid_len:1][kid:UTF-8][nonce:12][ct+tag]
        // v2: [version=2][recipient_kid_len:1][recipient_kid:UTF-8][eph_pub_len:2 BE][eph_pub][nonce:12][ct+tag]
        if (frame.Length < 2)
        {
            throw new InvalidOperationException(
                "Frame too short to read kid header.");
        }

        // L2: version byte must match a format we know how to read.
        // A future format bump (different field ordering, longer kid_len
        // prefix, etc.) MUST surface here — silently parsing an unknown
        // frame would emit a garbage `x-d2-encryption-kid` header that
        // ops use for archive-key triage, which is worse than a hard fail.
        var version = frame[0];

        if (version == EncryptionFrameLayout.CURRENT_VERSION)
        {
            var kidLen = frame[EncryptionFrameLayout.KID_LENGTH_OFFSET];

            if (frame.Length < EncryptionFrameLayout.KID_OFFSET + kidLen)
            {
                throw new InvalidOperationException(
                    "Frame too short for declared kid length.");
            }

            return Encoding.UTF8.GetString(
                frame.Slice(EncryptionFrameLayout.KID_OFFSET, kidLen));
        }

        if (version == SealedFrameLayout.CURRENT_VERSION)
        {
            var kidLen = frame[SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET];

            if (frame.Length < SealedFrameLayout.RECIPIENT_KID_OFFSET + kidLen)
            {
                throw new InvalidOperationException(
                    "Frame too short for declared kid length.");
            }

            return Encoding.UTF8.GetString(
                frame.Slice(SealedFrameLayout.RECIPIENT_KID_OFFSET, kidLen));
        }

        throw new InvalidOperationException(
            $"Unknown encryption frame version: {version}. Expected " +
            $"{EncryptionFrameLayout.CURRENT_VERSION} (symmetric) or " +
            $"{SealedFrameLayout.CURRENT_VERSION} (sealed).");
    }

    // The consumer service id that keys a sealed domain's IPayloadSealer /
    // IPayloadOpener. A sealed descriptor always carries a non-null
    // ConsumerService (the spec-derived catalog binds sealed-ness and the
    // consumer service together — one is never present without the other); a
    // null here is a generated-catalog contradiction that must fail loud rather
    // than silently resolve the null-keyed service.
    private static string ConsumerServiceKey(MqMessageDescriptor descriptor)
        => descriptor.ConsumerService
            ?? throw new InvalidOperationException(
                $"Message domain '{descriptor.Encryption}' resolved as sealed but "
                + "carries no consumer service — the generated encryption-domain "
                + "catalog is internally inconsistent.");
}

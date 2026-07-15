// -----------------------------------------------------------------------
// <copyright file="IForwardedJwtAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Request-scoped holder for the inbound forwarded JWT, structurally isolated
/// from the request context and its log / OpenTelemetry enrichment projection.
/// Populated by the auth surface (HTTP middleware / gRPC interceptor) after a
/// bearer token has passed validation; read by the outbound forwarding
/// credential so it can replay the token byte-for-byte on a cross-process hop.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately NOT a property of the request context. The enriched request
/// context is a broadly projected log/telemetry surface (its enricher emits
/// dozens of its fields on every request); the forwarded JWT must be
/// structurally excluded from that projection so its non-logging cannot regress
/// through an unrelated enricher change. The holder is a distinct type with a
/// distinct DI registration, reachable only by code that explicitly injects it —
/// never enumerable from a heterogeneous bag and never reached by the enricher,
/// which reads only the request context. A field-set-exclusion test pins that
/// the enrichment projection contains no forwarded-JWT field.
/// </para>
/// <para>
/// Registered request-scoped, so a fresh request scope starts with no captured
/// token (<see cref="Current"/> is <see langword="null"/>) and one request's
/// credential can never bleed into another.
/// </para>
/// </remarks>
public interface IForwardedJwtAccessor
{
    /// <summary>
    /// Gets the captured forwarded JWT for the current request, or
    /// <see langword="null"/> when none has been captured — a harmless endpoint,
    /// a pre-auth resolution, or a host that does not forward.
    /// </summary>
    ForwardedJwt? Current { get; }

    /// <summary>
    /// Captures the validated raw bearer for the current request. Called by the
    /// auth surface once per request, after the inbound JWT validator has
    /// accepted the token. A blank input is ignored (never stored); a second
    /// capture within the same request scope overwrites the first
    /// (last-write-wins) — in the real pipeline a request is validated once, so a
    /// second capture does not occur.
    /// </summary>
    /// <param name="rawBearer">The validated raw bearer string to retain.</param>
    void Capture(string rawBearer);
}

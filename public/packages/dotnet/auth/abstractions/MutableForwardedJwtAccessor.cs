// -----------------------------------------------------------------------
// <copyright file="MutableForwardedJwtAccessor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Default <see cref="IForwardedJwtAccessor"/> implementation — a plain
/// per-request mutable cell. Registered request-scoped by both
/// <c>AddD2AuthHttp()</c> and <c>AddD2AuthGrpc()</c>, so each request gets a
/// fresh instance with no captured token and no cross-request state.
/// </summary>
/// <remarks>
/// <para>
/// Holds no static state — request isolation is provided entirely by the
/// scoped DI lifetime. The capture path routes through
/// <see cref="ForwardedJwt.Create"/>, so a blank input is validated away and
/// never stored.
/// </para>
/// <para>
/// Public because the DI registration sites live in the separate transport
/// assemblies (<c>DcsvIo.D2.Auth.Http</c> / <c>DcsvIo.D2.Auth.Grpc</c>) — the
/// same cross-assembly-registration reason the analogous request-context
/// concrete impl is public. Consumers depend on <see cref="IForwardedJwtAccessor"/>,
/// not this type.
/// </para>
/// </remarks>
public sealed class MutableForwardedJwtAccessor : IForwardedJwtAccessor
{
    private ForwardedJwt? _current;

    /// <inheritdoc/>
    public ForwardedJwt? Current => _current;

    /// <inheritdoc/>
    public void Capture(string rawBearer)
    {
        // Create() validates (null/empty/whitespace -> fail); a failed wrapper is
        // never stored. A second capture in the same scope overwrites the first
        // (last-write-wins); the real pipeline validates once, so this is a
        // defensive, documented semantic rather than an expected occurrence.
        var result = ForwardedJwt.Create(rawBearer);

        if (result.Success)
            _current = result.Data;
    }
}

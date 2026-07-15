// -----------------------------------------------------------------------
// <copyright file="ClaimsToContextMapper.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Validation;

using System.Security.Claims;
using DcsvIo.D2.Context.Abstractions;

/// <summary>
/// Translates a validated <see cref="ClaimsPrincipal"/> (the output of
/// <see cref="JwtValidator"/>) into a populated
/// <see cref="MutableRequestContext"/>. Thin facade over the codegen-emitted
/// <c>MutableRequestContext.FromClaims</c> factory.
/// </summary>
/// <remarks>
/// <para>
/// Owned as a separate type rather than calling the codegen factory inline
/// from the validator for two reasons:
/// </para>
/// <list type="bullet">
///   <item><strong>Test seam</strong> — middleware / interceptor / future
///     consumers depend on this abstraction, not on the static factory; tests
///     can substitute a fake mapper to drive validator-only behaviors.</item>
///   <item><strong>Future enrichment hook</strong> — when transport-layer
///     middleware needs to layer in transport-derived facts after the JWT
///     pipeline (e.g. request-time fingerprint augmentation) the seam already
///     exists; no refactor of the validator required.</item>
/// </list>
/// <para>
/// <strong>Post-validation contract — intentional <c>IsAuthenticated</c>
/// override</strong>: the resulting context has
/// <see cref="MutableRequestContext.IsAuthenticated"/> set to
/// <see langword="true"/> explicitly (see <see cref="Map"/>). The codegen
/// factory <c>MutableRequestContext.FromClaims(ClaimsPrincipal)</c> already
/// derives this from <c>principal.Identity?.IsAuthenticated ?? false</c>, but
/// the explicit re-assignment here is a stronger contract: any context
/// returned from this mapper has been signature-validated AND
/// standard-claim-checked by <see cref="JwtValidator"/>, so authentication is
/// a settled fact — independent of whatever <see cref="ClaimsIdentity"/>
/// flag plumbing the upstream construction path happened to use. The
/// override guards against a future codegen / factory refactor that would
/// leave <c>IsAuthenticated</c> null / false on a verified principal, and
/// makes the post-validation invariant impossible to miss for any future
/// maintainer reading the pipeline.
/// </para>
/// <para>
/// This mapper is truly stateless and registered as a singleton.
/// </para>
/// </remarks>
internal sealed class ClaimsToContextMapper
{
    /// <summary>
    /// Maps a <see cref="ClaimsPrincipal"/> from a verified JWT into a
    /// populated <see cref="MutableRequestContext"/>.
    /// </summary>
    /// <param name="principal">The principal from a verified JWT.</param>
    /// <returns>
    /// A new <see cref="MutableRequestContext"/> with auth-derived fields
    /// populated and <see cref="MutableRequestContext.IsAuthenticated"/>
    /// set to <see langword="true"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="principal"/> is <see langword="null"/>.
    /// This is a programmer-error path — middleware / interceptor MUST hand
    /// a non-null principal coming out of <see cref="JwtValidator"/>.
    /// </exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Instance method by design — registered as a DI singleton + "
            + "future enrichment hook seam (see remarks on the class).")]
    public MutableRequestContext Map(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var ctx = MutableRequestContext.FromClaims(principal);

        // Make the post-validation invariant explicit. The codegen factory
        // already sets this from principal.Identity?.IsAuthenticated, but
        // re-stating it here guards against any future refactor that would
        // cause the factory to leave it null / false on a verified principal.
        ctx.IsAuthenticated = true;
        return ctx;
    }
}

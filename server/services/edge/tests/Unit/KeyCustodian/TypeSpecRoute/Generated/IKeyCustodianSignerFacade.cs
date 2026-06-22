// -----------------------------------------------------------------------
// <copyright file="IKeyCustodianSignerFacade.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// Hand-authored test-seam interface — NOT emitter output. The façade emitter
// emits the real I<Module>Api (IKeyCustodianApi, which exposes only GetJwksAsync);
// this 3-method signer-façade contract exists solely as the delegation target the
// TypeSpec route-policy / gRPC / mutual-TLS test harnesses fake implement. It is
// therefore correctly excluded from the generated-file byte-gate sweep — no emitter
// produces it.

// ReSharper disable once CheckNamespace -- the fixture façade deliberately lives in
// the .Generated.Facade namespace the real façade emitter targets for I<Module>Api,
// so harness consumers reference one façade namespace regardless of which façade they
// fake; the namespace intentionally does not track this file's folder.
namespace D2.Edge.Tests.TypeSpecRoute.Generated.Facade;

using D2.Edge.Tests.TypeSpecDto.Generated;
using D2.Shared.Result;

/// <summary>
/// Fixture façade interface for the KeyCustodian signer operations.
/// Distinct from the real <c>IKeyCustodianApi</c> (which only exposes
/// <c>GetJwksAsync</c>). Used only by the TypeSpec route-policy test harness.
/// </summary>
public interface IKeyCustodianSignerFacade
{
    /// <summary>Signs the payload with the specified key.</summary>
    /// <param name="input">The sign input (key id + payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sign result envelope.</returns>
    ValueTask<D2Result<SignOutput?>> SignAsync(SignInput input, CancellationToken ct = default);

    /// <summary>Signs the payload using a derived idempotency key.</summary>
    /// <param name="input">The sign input (key id + payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sign result envelope.</returns>
    ValueTask<D2Result<SignOutput?>> SignDerivedAsync(SignInput input, CancellationToken ct = default);

    /// <summary>Synthetic all-scopes op for coverage of RequireAllScopes route path.</summary>
    /// <param name="input">The sign input (key id + payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sign result envelope.</returns>
    ValueTask<D2Result<SignOutput?>> AllScopesAsync(SignInput input, CancellationToken ct = default);
}

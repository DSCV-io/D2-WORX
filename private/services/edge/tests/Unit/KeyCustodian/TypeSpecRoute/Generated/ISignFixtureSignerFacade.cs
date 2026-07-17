// -----------------------------------------------------------------------
// <copyright file="ISignFixtureSignerFacade.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// Hand-authored test-seam interface — NOT emitter output. The façade emitter
// emits the real I<Module>Api (ISignFixtureApi, which exposes only GetJwksAsync);
// this 3-method signer-façade contract exists solely as the delegation target the
// TypeSpec route-policy / gRPC / mutual-TLS test harnesses fake implement. It is
// therefore correctly excluded from the generated-file byte-gate sweep — no emitter
// produces it.

// ReSharper disable once CheckNamespace -- the fixture façade deliberately lives in
// the .Generated.Facade namespace the real façade emitter targets for I<Module>Api,
// so harness consumers reference one façade namespace regardless of which façade they
// fake; the namespace intentionally does not track this file's folder.
namespace DcsvIo.D2.Private.Edge.Tests.TypeSpecRoute.Generated.Facade;

using DcsvIo.D2.Private.Edge.Tests.TypeSpecDto.Generated;
using DcsvIo.D2.Result;

/// <summary>
/// Fixture façade interface for the SignFixture signer operations.
/// Distinct from the real <c>ISignFixtureApi</c> (which only exposes
/// <c>GetJwksAsync</c>). Used only by the TypeSpec route-policy test harness.
/// </summary>
public interface ISignFixtureSignerFacade
{
    /// <summary>Signs the payload with the specified key.</summary>
    /// <param name="input">The sign input (key id + payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sign result envelope.</returns>
    ValueTask<D2Result<SignFixtureOutput?>> SignFixtureAsync(SignFixtureInput input, CancellationToken ct = default);

    /// <summary>Signs the payload using a derived idempotency key.</summary>
    /// <param name="input">The sign input (key id + payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sign result envelope.</returns>
    ValueTask<D2Result<SignFixtureOutput?>> SignFixtureDerivedAsync(SignFixtureInput input, CancellationToken ct = default);

    /// <summary>Synthetic all-scopes op for coverage of RequireAllScopes route path.</summary>
    /// <param name="input">The sign input (key id + payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sign result envelope.</returns>
    ValueTask<D2Result<SignFixtureOutput?>> AllScopesAsync(SignFixtureInput input, CancellationToken ct = default);
}

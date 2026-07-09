// -----------------------------------------------------------------------
// <copyright file="InProcessSealingClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Sealing;

using D2.Edge.KeyCustodian.Client.Facade;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.Time;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// In-process seal-keyring fetch source for a co-hosted PRODUCER (a module inside the Edge
/// host that publishes sealed messages). Establishes the in-process-module plane on a fresh
/// scoped request-context — <see cref="RequestOrigin.InProcessModule"/>,
/// <c>ImmediateCaller = callingModuleId</c>, the <c>internal.kc.seal.encrypt</c> scope — then
/// invokes the real KeyCustodian leaf to fetch a recipient's PUBLIC sealing keyring. The
/// SEALER (encrypt) arm is the only in-process seal capability; the own-PRIVATE-key op is
/// structurally unsupported here (decrypt is CrossProcessHop-only — a co-hosted module never
/// opens sealed frames in-process; it takes the cross-process gRPC opener arm).
/// </summary>
/// <remarks>
/// Lives in the module's App project (not the client package): it composes the leaf
/// <see cref="IKeyCustodianApi"/>, which the client package cannot reference under the
/// dependency law. The calling module id is an explicit registration parameter (no ambient
/// guessing — fail-closed: the host names itself).
/// </remarks>
internal sealed class InProcessSealingClient : ISealingClient
{
    /// <summary>
    /// The in-host module being entered — KeyCustodian owns the sealing surface.
    /// </summary>
    private const string _TARGET_MODULE_ID = "key-custodian";

    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly IClock r_clock;
    private readonly string r_callingModuleId;

    /// <summary>Initializes a new <see cref="InProcessSealingClient"/>.</summary>
    /// <param name="scopeFactory">Factory for the per-fetch DI scope.</param>
    /// <param name="clock">Clock used to timestamp the established module hop.</param>
    /// <param name="callingModuleId">The id of the module/host making the in-process call.</param>
    public InProcessSealingClient(
        IServiceScopeFactory scopeFactory, IClock clock, string callingModuleId)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(clock);
        callingModuleId.ThrowIfFalsey();
        r_scopeFactory = scopeFactory;
        r_clock = clock;
        r_callingModuleId = callingModuleId;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Structurally unsupported: no in-process OPENER source exists anywhere — sealed
    /// decrypt is CrossProcessHop-only (the KC own-seal-private op is mTLS-peer-selected). A
    /// co-hosted module that must open sealed frames takes the cross-process
    /// <c>AddD2SealedEncryptionViaKeyCustodian</c> opener arm, never an in-process unwrap.
    /// This throw enforces that no code path can accidentally wire an in-process opener.
    /// </remarks>
    public ValueTask<D2Result<RecipientPrivateKeyring>> GetOwnPrivateKeyringAsync(
        string ownServiceId, CancellationToken ct = default)
        => throw new NotSupportedException(
            "In-process sealed opening is structurally disallowed: sealed decrypt is "
            + "CrossProcessHop-only (the private-key op is selected by the authenticated mTLS "
            + "peer). Use AddD2SealedEncryptionViaKeyCustodian for the opener arm.");

    /// <inheritdoc />
    public async ValueTask<D2Result<RecipientPublicKeyring>> GetPublicKeyringAsync(
        string recipientServiceId, CancellationToken ct = default)
    {
        if (recipientServiceId.Falsey())
            return D2Result<RecipientPublicKeyring>.ValidationFailed();

        await using var scope = r_scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();
        context.EstablishInProcessModule(r_callingModuleId, _TARGET_MODULE_ID, r_clock);
        context.Scopes = new HashSet<string>(StringComparer.Ordinal)
        {
            Scopes.Internal.Kc.Seal.Encrypt,
        };

        var api = scope.ServiceProvider.GetRequiredService<IKeyCustodianApi>();
        var leafResult = await api
            .GetOrLazyProvisionSealPublicKeyAsync(
                new GetOrLazyProvisionSealPublicKeyInput(recipientServiceId), ct)
            .ConfigureAwait(false);

        var result = SealingOutputMapper.ToPublicKeyringResult(
            leafResult, leafResult.Data, recipientServiceId);
        SealingMetrics.RecordFetch(SealDomainName.For(recipientServiceId), result.Success);

        return result;
    }
}

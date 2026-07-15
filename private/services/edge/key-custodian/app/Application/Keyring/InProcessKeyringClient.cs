// -----------------------------------------------------------------------
// <copyright file="InProcessKeyringClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Keyring;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Time;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// In-process keyring fetch source for a co-hosted consumer (a module inside the Edge
/// host). Establishes the in-process-module plane on a fresh scoped request-context â€”
/// <see cref="RequestOrigin.InProcessModule"/>, <c>ImmediateCaller = callingModuleId</c>,
/// the <c>internal.kc.keyring</c> scope â€” then invokes the real KeyCustodian leaf. The
/// leaf's fail-closed <c>AuthorizeKeyringFetch</c> rule authorizes (or denies) the fetch;
/// this source never re-derives that authority.
/// </summary>
/// <remarks>
/// Lives in the module's App project (not the client package): it composes the leaf
/// <see cref="IKeyCustodianApi"/>, and the client package cannot reference App under the
/// dependency law. The calling module id is an explicit registration parameter (no ambient
/// guessing â€” fail-closed: the host names itself). The fetch seam is
/// <see langword="internal"/>: it is the authorized fetch path, not an injectable
/// "dump any keyring" service.
/// </remarks>
internal sealed class InProcessKeyringClient : IKeyringClient
{
    /// <summary>
    /// The in-host module being entered â€” KeyCustodian owns the keyring surface.
    /// </summary>
    private const string _TARGET_MODULE_ID = "key-custodian";

    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly IClock r_clock;
    private readonly string r_callingModuleId;

    /// <summary>Initializes a new <see cref="InProcessKeyringClient"/>.</summary>
    /// <param name="scopeFactory">Factory for the per-fetch DI scope.</param>
    /// <param name="clock">Clock used to timestamp the established module hop.</param>
    /// <param name="callingModuleId">The id of the module/host making the in-process call.</param>
    public InProcessKeyringClient(
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
    public async ValueTask<D2Result<PayloadCryptoKeyring>> GetKeyringAsync(
        string domain, CancellationToken ct = default)
    {
        if (domain.Falsey())
            return D2Result<PayloadCryptoKeyring>.ValidationFailed();

        await using var scope = r_scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();
        context.EstablishInProcessModule(r_callingModuleId, _TARGET_MODULE_ID, r_clock);
        context.Scopes = new HashSet<string>(StringComparer.Ordinal)
        {
            ProductScopes.Internal.Kc.Keyring,
        };

        var api = scope.ServiceProvider.GetRequiredService<IKeyCustodianApi>();
        var leafResult = await api
            .GetKeyringAsync(new GetKeyringInput(domain), ct)
            .ConfigureAwait(false);

        var result = KeyringOutputMapper.ToKeyringResult(leafResult, leafResult.Data);
        KeyringMetrics.RecordFetch(domain, result.Success);

        return result;
    }
}

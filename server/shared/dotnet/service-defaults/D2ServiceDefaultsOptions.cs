// -----------------------------------------------------------------------
// <copyright file="D2ServiceDefaultsOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceDefaults;

using D2.Shared.AspNetCore;
using D2.Shared.Auth;
using D2.Shared.Caching;
using D2.Shared.Logging;
using D2.Shared.Telemetry;

/// <summary>
/// Options surface for
/// <see cref="ServiceDefaultsServiceCollectionExtensions"/>'s
/// <c>AddD2ServiceDefaults</c>. Carries this aggregator's own opt-out
/// flags PLUS pure pass-through <see cref="Action{T}"/> delegates that
/// forward to each underlying lib's own options surface — the aggregator
/// itself owns ZERO field-level configuration knowledge so a duplicated
/// alias never drifts from the owning lib's actual options shape.
/// </summary>
/// <remarks>
/// <para>
/// The opt-out flags exist because the aggregator auto-wires components
/// that the >95% case wants but a small set of services (test hosts,
/// dry-run admin tools, services with bespoke wiring) do not. Setting a
/// flag to <c>true</c> means the corresponding <c>AddD2*</c> call is
/// skipped entirely; the caller is then responsible for wiring (or not
/// wiring) the corresponding component themselves.
/// </para>
/// <para>
/// The pass-through <see cref="Action{T}"/> delegates are NOT a duplicated
/// options surface — each one is forwarded VERBATIM to the owning lib's
/// existing <c>Action&lt;TFromOwningLib&gt;?</c> parameter. New options on
/// any owning lib show up at the aggregator's call site for free, with
/// zero aggregator-side maintenance.
/// </para>
/// </remarks>
public sealed class D2ServiceDefaultsOptions
{
    // === Opt-out flags ===

    /// <summary>
    /// Gets or sets a value indicating whether the aggregator should skip
    /// auto-wiring the inbound auth runtime
    /// (<c>AddD2Auth</c> + <c>AddD2AuthHttp</c> + <c>AddD2AuthGrpc</c>).
    /// Default <c>false</c> — auto-wire because >95% of D² services are
    /// authenticated.
    /// </summary>
    /// <remarks>
    /// When <c>false</c> (the default),
    /// <see cref="AuthConfigure"/> MUST be non-null — the aggregator throws
    /// <see cref="System.InvalidOperationException"/> at host build
    /// otherwise. Set this flag to <c>true</c> to opt out of auth wiring
    /// entirely (test hosts, anonymous-only admin endpoints).
    /// </remarks>
    public bool SkipAuthAutoWiring { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the aggregator should skip
    /// auto-wiring the in-process L1 cache (<c>AddD2LocalCache</c>).
    /// Default <c>false</c> — auto-wire because the local cache has zero
    /// external dependencies and most code paths benefit.
    /// </summary>
    public bool SkipLocalCacheAutoWiring { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the aggregator should skip
    /// the BCL standard HttpClient resilience handler
    /// (<c>ConfigureHttpClientDefaults(http =&gt; http.AddStandardResilienceHandler())</c>).
    /// Default <c>false</c> — auto-wire the standard
    /// retry + circuit-break + timeout handler for ALL named HttpClients
    /// in the host, mirroring .NET Aspire's service-defaults behavior.
    /// </summary>
    public bool SkipHttpClientResilienceDefaults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the aggregator should skip
    /// the deny-by-default auth endpoint guard
    /// (<c>AddD2AuthEndpointGuard</c>). Default <c>false</c> — guard ON,
    /// because the overwhelming majority of D² services are authenticated
    /// and every endpoint must carry a declared auth intent.
    /// </summary>
    /// <remarks>
    /// The guard fails host startup when any mapped
    /// <see cref="Microsoft.AspNetCore.Routing.RouteEndpoint"/> lacks a
    /// declared auth intent (<c>RequireAnyScope</c> /
    /// <c>RequireAllScopes</c> / <c>MarkAsD2HarmlessEndpoint</c> fluent
    /// call, or the corresponding gRPC attribute). Set this flag to
    /// <c>true</c> to opt out (test hosts that register synthetic endpoints
    /// without auth declarations, anonymous-only admin tools).
    /// </remarks>
    public bool SkipAuthEndpointGuard { get; set; }

    // === Per-component options pass-through (each typed Action<TFromOwningLib>?) ===

    /// <summary>
    /// Gets or sets the optional pass-through to <c>AddD2Logging</c>'s
    /// configure callback — runs AFTER the env-derived defaults so the
    /// override always wins on conflict.
    /// </summary>
    public Action<D2LoggingOptions>? LoggingConfigure { get; set; }

    /// <summary>
    /// Gets or sets the optional pass-through to <c>AddD2Telemetry</c>'s
    /// configure callback — runs AFTER the env-derived defaults so the
    /// override always wins on conflict.
    /// </summary>
    public Action<D2TelemetryOptions>? TelemetryConfigure { get; set; }

    /// <summary>
    /// Gets or sets the optional pass-through to <c>AddD2Cors</c>'s
    /// configure callback — runs AFTER the env-derived defaults so the
    /// override always wins on conflict.
    /// </summary>
    public Action<D2CorsOptions>? CorsConfigure { get; set; }

    /// <summary>
    /// Gets or sets the optional pass-through to
    /// <c>AddD2ProblemDetails</c>'s configure callback.
    /// </summary>
    public Action<D2ProblemDetailsOptions>? ProblemDetailsConfigure { get; set; }

    /// <summary>
    /// Gets or sets the optional pass-through to
    /// <c>UseD2SecurityHeaders</c>'s configure callback (applied at
    /// pipeline-installation time, NOT at service registration).
    /// </summary>
    public Action<D2SecurityHeadersOptions>? SecurityHeadersConfigure { get; set; }

    /// <summary>
    /// Gets or sets the optional pass-through to
    /// <c>UseD2InfrastructureBypass</c>'s configure callback (applied at
    /// pipeline-installation time, NOT at service registration).
    /// </summary>
    public Action<D2InfrastructureBypassOptions>? InfrastructureBypassConfigure { get; set; }

    /// <summary>
    /// Gets or sets the optional pass-through to <c>AddD2LocalCache</c>'s
    /// configure callback. Ignored when
    /// <see cref="SkipLocalCacheAutoWiring"/> is <c>true</c>.
    /// </summary>
    public Action<LocalCacheOptions>? LocalCacheConfigure { get; set; }

    /// <summary>
    /// Gets or sets the required pass-through to <c>AddD2Auth</c>'s
    /// configure callback (the underlying lib has no parameterless
    /// overload — every caller MUST populate
    /// <see cref="AuthOptions.Issuer"/> +
    /// <see cref="AuthOptions.Audience"/>). Ignored when
    /// <see cref="SkipAuthAutoWiring"/> is <c>true</c>; required (non-null)
    /// otherwise — the aggregator throws
    /// <see cref="System.InvalidOperationException"/> at host build when
    /// this is null AND
    /// <see cref="SkipAuthAutoWiring"/> is <c>false</c>.
    /// </summary>
    public Action<AuthOptions>? AuthConfigure { get; set; }
}

// -----------------------------------------------------------------------
// <copyright file="AddD2ForwardedJwtOutboundTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Http;
using D2.Shared.Auth.Outbound;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Coverage for <see cref="AuthOutboundServiceCollectionExtensions.AddD2ForwardedJwtOutbound"/>
/// — the host's residual config registration for the forwarded-JWT outbound
/// factor. Verifies it is null-guarded, idempotent, and — composed with the
/// inbound transport (which owns the holder + the ambient adapter) — that a
/// forwarding host resolves EVERYTHING the per-channel forwarding credential
/// reaches for: the ambient-scope accessor (singleton) and the request-scoped
/// holder (in a scope).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddD2ForwardedJwtOutboundTests
{
    // ------------------------------------------------------------------
    // T-NULL — null services → ArgumentNullException.
    // ------------------------------------------------------------------
    [Fact]
    public void AddD2ForwardedJwtOutbound_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2ForwardedJwtOutbound();

        act.Should().Throw<ArgumentNullException>();
    }

    // ------------------------------------------------------------------
    // T-RESOLVE — after AddD2ForwardedJwtOutbound() the options pipeline resolves;
    // it does NOT (by itself) register the holder or the ambient adapter (those
    // come from the inbound transport — documented contract). Idempotent.
    // ------------------------------------------------------------------
    [Fact]
    public void AddD2ForwardedJwtOutbound_Idempotent_OptionsResolve_NoInboundConcernsRegistered()
    {
        var services = new ServiceCollection();

        services.AddD2ForwardedJwtOutbound();
        services.AddD2ForwardedJwtOutbound(); // twice — must not double-trouble

        var provider = services.BuildServiceProvider();

        provider.GetService<Microsoft.Extensions.Options.IOptions<AuthOutboundOptions>>()
            .Should().NotBeNull("the options pipeline is present");

        // It deliberately registers NEITHER the holder NOR the ambient adapter —
        // the inbound transport owns them. Proving the minimal posture.
        provider.GetService<IForwardedJwtAccessor>().Should().BeNull(
            "the holder is registered by the inbound transport, not this call");
        provider.GetService<IAmbientRequestScopeAccessor>().Should().BeNull(
            "the ambient adapter is registered by the inbound transport, not this call");
    }

    // ------------------------------------------------------------------
    // T-RESOLVE-HOLDER-VIA-INBOUND — a composed forwarding host (inbound HTTP +
    // outbound) resolves the ambient adapter (singleton) AND, in a request scope,
    // the forwarded-JWT holder — i.e. everything the forwarding credential reaches
    // for. GetRequiredService discipline (§1.3): resolve every seam, not just check
    // descriptor presence.
    // ------------------------------------------------------------------
    [Fact]
    public void ComposedForwardingHost_ResolvesAmbientAdapterAndScopedHolder()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services
            .AddD2Auth(o =>
            {
                o.Issuer = new Uri("https://edge.internal");
                o.Audience = "d2.internal";
            })
            .AddD2AuthHttp();
        services.AddD2AuthOutbound(o =>
        {
            o.Issuer = "https://edge.internal";
            o.ClientId = "svc";
            o.ClientSecret = "secret";
        });
        services.AddD2ForwardedJwtOutbound();

        var provider = services.BuildServiceProvider();

        // Singleton: the ambient-scope adapter the channel-build resolves.
        var ambient = provider.GetRequiredService<IAmbientRequestScopeAccessor>();
        ambient.Should().BeOfType<D2.Shared.Auth.Http.Ambient.HttpContextAmbientRequestScopeAccessor>();

        // IHttpContextAccessor (the adapter's backing seam) resolves too.
        provider.GetRequiredService<IHttpContextAccessor>().Should().NotBeNull();

        // Scoped: the request-scoped holder the credential reads per call.
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>()
            .Should().BeOfType<MutableForwardedJwtAccessor>();
    }

    // ------------------------------------------------------------------
    // End-to-end through the adapter: with an HttpContext on the ambient accessor
    // whose RequestServices is a scope holding a captured token, the adapter
    // surfaces that scope so the holder is reachable — the read-back door the
    // forwarding credential uses. Proves the Option-B port+adapter wiring works
    // through the real IHttpContextAccessor.
    // ------------------------------------------------------------------
    [Fact]
    public void AmbientAdapter_SurfacesRequestScopeHoldingToken_ViaHttpContext()
    {
        const string captured_jwt = "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJFMkUifQ.sig_e2e_04";

        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddD2Auth(o =>
            {
                o.Issuer = new Uri("https://edge.internal");
                o.Audience = "d2.internal";
            })
            .AddD2AuthHttp();

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>().Capture(captured_jwt);

        // Put an HttpContext whose RequestServices is the populated scope onto the
        // ambient accessor (what the AspNetCore pipeline does per request).
        var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };

        var ambient = provider.GetRequiredService<IAmbientRequestScopeAccessor>();

        ambient.Current.Should().BeSameAs(scope.ServiceProvider);
        var holder = ambient.Current!.GetRequiredService<IForwardedJwtAccessor>();
        holder.Current.Should().NotBeNull();
        holder.Current!.Value.RevealForForwarding().Should().Be(captured_jwt);
    }
}

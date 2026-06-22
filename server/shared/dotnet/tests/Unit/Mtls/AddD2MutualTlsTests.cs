// -----------------------------------------------------------------------
// <copyright file="AddD2MutualTlsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Mtls;

using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.AspNetCore.Mtls;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Coverage for <c>AddD2MutualTls</c> + <see cref="D2MutualTlsOptions"/>: the
/// fail-loud validation (enabled-but-empty allowed-set / missing anchors), the
/// safe-by-default disabled path (no Kestrel client-cert config), the
/// require-certificate Kestrel wiring when enabled, and the validator-seam
/// resolvability (every registered seam resolves via
/// <c>GetRequiredService&lt;&gt;</c>).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AddD2MutualTlsTests
{
    // -----------------------------------------------------------------------
    // Fail-loud — enabled but misconfigured
    // -----------------------------------------------------------------------

    [Fact]
    public void Enabled_EmptyAllowedWorkloads_ThrowsAtValidation()
    {
        using var provider = BuildProvider(o =>
        {
            o.Enabled = true;
            o.AllowedWorkloads = [];
            o.TrustAnchorsProvider = AnAnchor;
        });

        var options = provider.GetRequiredService<IOptions<D2MutualTlsOptions>>();
        var act = () => _ = options.Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Enabled_WhitespaceWorkloadEntry_ThrowsAtValidation()
    {
        using var provider = BuildProvider(o =>
        {
            o.Enabled = true;
            o.AllowedWorkloads = ["edge", "   "];
            o.TrustAnchorsProvider = AnAnchor;
        });

        var options = provider.GetRequiredService<IOptions<D2MutualTlsOptions>>();
        var act = () => _ = options.Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Enabled_NoTrustAnchorsProvider_ThrowsAtValidation()
    {
        using var provider = BuildProvider(o =>
        {
            o.Enabled = true;
            o.AllowedWorkloads = ["edge"];
            o.TrustAnchorsProvider = null;
        });

        var options = provider.GetRequiredService<IOptions<D2MutualTlsOptions>>();
        var act = () => _ = options.Value;

        act.Should().Throw<OptionsValidationException>();
    }

    // -----------------------------------------------------------------------
    // Fully-configured enabled host validates + wires Kestrel require-certificate
    // -----------------------------------------------------------------------

    [Fact]
    public void Enabled_FullyConfigured_ValidatesAndResolves()
    {
        using var provider = BuildProvider(o =>
        {
            o.Enabled = true;
            o.AllowedWorkloads = ["edge"];
            o.TrustAnchorsProvider = AnAnchor;
        });

        var options = provider.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value;
        options.Enabled.Should().BeTrue();
        options.AllowedWorkloads.Should().ContainSingle().Which.Should().Be("edge");

        // The validator seam resolves.
        provider.GetRequiredService<SpiffeSanPeerValidator>().Should().NotBeNull();
    }

    [Fact]
    public void Enabled_ConfiguresKestrelRequireCertificate_AndValidationCallback()
    {
        using var provider = BuildProvider(o =>
        {
            o.Enabled = true;
            o.AllowedWorkloads = ["edge"];
            o.TrustAnchorsProvider = AnAnchor;
        });

        var kestrel = ResolveKestrelHttpsDefaults(provider);

        kestrel.ClientCertificateMode.Should().Be(ClientCertificateMode.RequireCertificate);
        kestrel.ClientCertificateValidation.Should().NotBeNull(
            "the default-deny peer-validation callback must be installed when enabled");
    }

    // -----------------------------------------------------------------------
    // Disabled — safe-by-default (no Kestrel client-cert config)
    // -----------------------------------------------------------------------

    [Fact]
    public void Disabled_DoesNotConfigureKestrelClientCertificate()
    {
        using var provider = BuildProvider(o => o.Enabled = false);

        var kestrel = ResolveKestrelHttpsDefaults(provider);

        kestrel.ClientCertificateMode.Should().Be(ClientCertificateMode.NoCertificate);
        kestrel.ClientCertificateValidation.Should().BeNull();
    }

    [Fact]
    public void Disabled_EmptyConfig_DoesNotThrow()
    {
        // A disabled host with no allowed-workloads / no anchors must validate fine
        // — the fail-loud gates apply only when Enabled.
        using var provider = BuildProvider(o => o.Enabled = false);

        var options = provider.GetRequiredService<IOptions<D2MutualTlsOptions>>();
        var act = () => _ = options.Value;

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    // Defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void Defaults_DisabledWithEmptyWorkloadsAndNoAnchors()
    {
        // The trust domain is fixed at d2.internal by the SPIFFE grammar — not
        // configurable on options. The defaults are simply: disabled, empty
        // allowed-set, no anchors.
        var options = new D2MutualTlsOptions();

        options.Enabled.Should().BeFalse();
        options.AllowedWorkloads.Should().BeEmpty();
        options.TrustAnchorsProvider.Should().BeNull();
    }

    private static ServiceProvider BuildProvider(Action<D2MutualTlsOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2MutualTls(configure);

        return services.BuildServiceProvider();
    }

    private static X509Certificate2Collection AnAnchor()
    {
        // A standalone self-signed cert is a sufficient trust-anchor for the options
        // validation + Kestrel-wiring tests (these don't validate a real leaf, which
        // is the SpiffeSanPeerValidator matrix's job). Loading from RawData yields an
        // independent handle, so nothing here is captured-disposed.
        using var key = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            "CN=Test Anchor", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
        using var anchor = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));

        return [X509CertificateLoader.LoadCertificate(anchor.RawData)];
    }

    private static HttpsConnectionAdapterOptions ResolveKestrelHttpsDefaults(
        ServiceProvider provider)
    {
        // Run the IConfigureOptions<KestrelServerOptions> chain, then capture the
        // ConfigureHttpsDefaults action's effect on a probe options instance.
        var kestrelOptions = provider.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        var probe = new HttpsConnectionAdapterOptions();
        var httpsAction = GetHttpsDefaultsAction(kestrelOptions);
        httpsAction?.Invoke(probe);

        return probe;
    }

    private static Action<HttpsConnectionAdapterOptions>? GetHttpsDefaultsAction(
        KestrelServerOptions options)
    {
        // KestrelServerOptions stores the ConfigureHttpsDefaults action in a private
        // field. Find it by FIELD TYPE (Action<HttpsConnectionAdapterOptions>) rather
        // than by name so the lookup survives an internal rename — the test asserts
        // the require-certificate wiring without standing up a real socket (the
        // end-to-end socket proof is the S5 harness).
        var field = typeof(KestrelServerOptions)
            .GetFields(
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(f =>
                f.FieldType == typeof(Action<HttpsConnectionAdapterOptions>));

        return field?.GetValue(options) as Action<HttpsConnectionAdapterOptions>;
    }
}

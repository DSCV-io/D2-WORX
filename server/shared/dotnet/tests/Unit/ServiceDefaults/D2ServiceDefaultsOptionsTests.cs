// -----------------------------------------------------------------------
// <copyright file="D2ServiceDefaultsOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.Auth;
using D2.Shared.Caching;
using D2.Shared.Logging;
using D2.Shared.ServiceDefaults;
using D2.Shared.Telemetry;
using Xunit;

public sealed class D2ServiceDefaultsOptionsTests
{
    [Fact]
    public void Defaults_AllOptOutFlagsAreFalse()
    {
        var opts = new D2ServiceDefaultsOptions();
        opts.SkipAuthAutoWiring.Should().BeFalse();
        opts.SkipLocalCacheAutoWiring.Should().BeFalse();
        opts.SkipAuthEndpointGuard.Should().BeFalse();
    }

    [Fact]
    public void Defaults_AllConfigureCallbacksAreNull()
    {
        var opts = new D2ServiceDefaultsOptions();
        opts.LoggingConfigure.Should().BeNull();
        opts.TelemetryConfigure.Should().BeNull();
        opts.CorsConfigure.Should().BeNull();
        opts.ProblemDetailsConfigure.Should().BeNull();
        opts.SecurityHeadersConfigure.Should().BeNull();
        opts.InfrastructureBypassConfigure.Should().BeNull();
        opts.LocalCacheConfigure.Should().BeNull();
        opts.AuthConfigure.Should().BeNull();
    }

    [Fact]
    public void Type_IsSealed()
    {
        typeof(D2ServiceDefaultsOptions).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void OptOutFlags_RoundTripViaSetters()
    {
        var opts = new D2ServiceDefaultsOptions
        {
            SkipAuthAutoWiring = true,
            SkipLocalCacheAutoWiring = true,
            SkipAuthEndpointGuard = true,
        };

        opts.SkipAuthAutoWiring.Should().BeTrue();
        opts.SkipLocalCacheAutoWiring.Should().BeTrue();
        opts.SkipAuthEndpointGuard.Should().BeTrue();
    }

    [Fact]
    public void ConfigureCallbacks_RoundTripViaSetters()
    {
        Action<D2LoggingOptions> logging = _ => { };
        Action<D2TelemetryOptions> telemetry = _ => { };
        Action<D2CorsOptions> cors = _ => { };
        Action<D2ProblemDetailsOptions> problem = _ => { };
        Action<D2SecurityHeadersOptions> security = _ => { };
        Action<D2InfrastructureBypassOptions> bypass = _ => { };
        Action<LocalCacheOptions> localCache = _ => { };
        Action<AuthOptions> auth = _ => { };

        var opts = new D2ServiceDefaultsOptions
        {
            LoggingConfigure = logging,
            TelemetryConfigure = telemetry,
            CorsConfigure = cors,
            ProblemDetailsConfigure = problem,
            SecurityHeadersConfigure = security,
            InfrastructureBypassConfigure = bypass,
            LocalCacheConfigure = localCache,
            AuthConfigure = auth,
        };

        opts.LoggingConfigure.Should().BeSameAs(logging);
        opts.TelemetryConfigure.Should().BeSameAs(telemetry);
        opts.CorsConfigure.Should().BeSameAs(cors);
        opts.ProblemDetailsConfigure.Should().BeSameAs(problem);
        opts.SecurityHeadersConfigure.Should().BeSameAs(security);
        opts.InfrastructureBypassConfigure.Should().BeSameAs(bypass);
        opts.LocalCacheConfigure.Should().BeSameAs(localCache);
        opts.AuthConfigure.Should().BeSameAs(auth);
    }
}

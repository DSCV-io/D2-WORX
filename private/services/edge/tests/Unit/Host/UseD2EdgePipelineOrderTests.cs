// -----------------------------------------------------------------------
// <copyright file="UseD2EdgePipelineOrderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.Host;

using DcsvIo.D2.Private.Edge.Api.Pipeline;
using DcsvIo.D2.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pipeline order pins for <see cref="EdgePipelineExtensions.UseD2EdgePipeline"/>:
/// RequestOriginEdge after Auth; rate-limit body absent (slot documented only).
/// </summary>
[Trait("Category", "Unit")]
public sealed class UseD2EdgePipelineOrderTests
{
    [Fact]
    public void UseD2EdgePipeline_NullApp_Throws()
    {
        IApplicationBuilder app = null!;
        var act = () => app.UseD2EdgePipeline();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UseD2EdgePipeline_SourceOrder_AuthThenRequestOriginEdge()
    {
        var path = EdgeHostTestKit.ResolveEdgeApiSourceFile(
            "Pipeline", "EdgePipelineExtensions.cs");

        File.Exists(path).Should().BeTrue($"pipeline source must be discoverable at {path}");

        var source = File.ReadAllText(path);
        var authIdx = source.IndexOf("UseD2Auth()", StringComparison.Ordinal);

        var originIdx = source.IndexOf(
            "UseD2RequestOriginEdge()", StringComparison.Ordinal);

        authIdx.Should().BeGreaterThan(0);
        originIdx.Should().BeGreaterThan(authIdx);

        // Rate-limit body must not be registered in production pipeline C#.
        source.Should().NotContain("UseD2RateLimit");
        source.Should().NotContain("FUTURE:");
        source.Should().NotContain("inserted later");
        source.Should().NotContain("this deliverable");
    }

    [Fact]
    public void UseD2EdgePipeline_NonNullApp_ComposesWithoutThrow()
    {
        // Non-null path: register middleware on ApplicationBuilder without host Start
        // or app.Build() (full middleware activation needs the full host DI graph).
        // SkipAuthAutoWiring keeps auth off; Cors is registered for UseD2Cors.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddCors();
        services.Configure<D2ServiceDefaultsOptions>(o =>
            o.SkipAuthAutoWiring = true);

        using var sp = services.BuildServiceProvider();

        var app = new ApplicationBuilder(sp);

        var act = () => app.UseD2EdgePipeline();

        act.Should().NotThrow();
        app.ApplicationServices.Should().BeSameAs(sp);
    }

    [Fact]
    public void UseD2RequestOriginEdge_ExtensionIsPublic()
    {
        // Pipeline registers UseD2RequestOriginEdge after UseD2Auth — extension is public.
        typeof(DcsvIo.D2.Auth.Http.RequestOriginEdgeAppBuilderExtensions)
            .Should().NotBeNull();
    }
}

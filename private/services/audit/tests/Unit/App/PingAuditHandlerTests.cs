// -----------------------------------------------------------------------
// <copyright file="PingAuditHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.Tests.Unit.App;

using System.Net;
using D2.Audit.App.Application.Handlers.Queries.PingAudit;
using D2.Audit.Client.Ping;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// NIE handler: typed <c>ServiceUnavailable</c> — never throws.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PingAuditHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsServiceUnavailableNie()
    {
        var handler = CreateHandler();
        var result = await handler.HandleAsync(new PingAuditInput());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotThrowNotImplementedException()
    {
        var handler = CreateHandler();
        var act = async () => await handler.HandleAsync(new PingAuditInput());

        await act.Should().NotThrowAsync();
    }

    private static IPingAuditHandler CreateHandler()
    {
        // Construct directly with a real HandlerContext — no full DI graph.
        // Replace-trigger: live host DI resolves IPingAuditHandler via AddD2AuditApp.
        var ctx = new HandlerContext<PingAuditHandler>(
            new MutableRequestContext(),
            NullLogger<PingAuditHandler>.Instance);

        return new PingAuditHandler(ctx);
    }
}

// -----------------------------------------------------------------------
// <copyright file="AuthenticatedHub.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.SignalR.Hubs;

using D2.Shared.Handler.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// Authenticated SignalR hub — requires JWT Bearer authorization.
///
/// On connect, auto-subscribes the connection to channel groups derived
/// from JWT claims: <c>user:{userId}</c> and <c>org:{targetOrgId}</c>.
/// Push-only for F6 — no client-invokable methods.
/// </summary>
[Authorize]
public partial class AuthenticatedHub : Hub
{
    private readonly ILogger<AuthenticatedHub> r_logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticatedHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public AuthenticatedHub(ILogger<AuthenticatedHub> logger)
    {
        r_logger = logger;
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(JwtClaimTypes.SUB)?.Value;
        var orgId = Context.User?.FindFirst(JwtClaimTypes.ORG_ID)?.Value;

        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        if (orgId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"org:{orgId}");
        }

        LogClientConnected(r_logger, Context.ConnectionId, userId ?? "(none)", orgId ?? "(none)");

        await base.OnConnectedAsync();
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // userId/orgId at disconnect: Context.User survives the disconnect call
        // (the principal isn't torn down until after this hook returns), so we
        // can extract the same claims used at connect time. Falls back to "(none)"
        // for connections that weren't authenticated to begin with.
        var userId = Context.User?.FindFirst(JwtClaimTypes.SUB)?.Value;
        var orgId = Context.User?.FindFirst(JwtClaimTypes.ORG_ID)?.Value;

        LogClientDisconnected(r_logger, Context.ConnectionId, userId ?? "(none)", orgId ?? "(none)");

        await base.OnDisconnectedAsync(exception);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Client connected: {ConnectionId}, userId={UserId}, orgId={OrgId}")]
    private static partial void LogClientConnected(ILogger logger, string connectionId, string userId, string orgId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client disconnected: {ConnectionId}, userId={UserId}, orgId={OrgId}")]
    private static partial void LogClientDisconnected(ILogger logger, string connectionId, string userId, string orgId);
}

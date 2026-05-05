// -----------------------------------------------------------------------
// <copyright file="TestAuthContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthContext;

using System;
using System.Collections.Generic;
using D2.Shared.Auth.Abstractions;
using D2.Shared.AuthContext.Abstractions;

/// <summary>
/// In-memory <see cref="IAuthContext"/> for adversarial extension-method tests.
/// Init-only properties so each test can construct adversarial shapes without
/// the full <c>MutableRequestContext</c> ceremony.
/// </summary>
internal sealed class TestAuthContext : IAuthContext
{
    public bool? IsAuthenticated { get; init; }

    public IReadOnlyList<string> Audience { get; init; } = [];

    public Guid? SessionId { get; init; }

    public DateTimeOffset? TokenIssuedAt { get; init; }

    public DateTimeOffset? TokenExpiresAt { get; init; }

    public IReadOnlyList<ActorEntry> ActorChain { get; init; } = [];

    public string? Subject { get; init; }

    public Guid? UserId { get; init; }

    public string? Username { get; init; }

    public string? RequestedByClientId { get; init; }

    public string? ImmediateCallerClientId { get; init; }

    public string? OriginatingClientId { get; init; }

    public bool? IsServiceIdentity { get; init; }

    public Guid? OrgId { get; init; }

    public string? OrgName { get; init; }

    public OrgType? OrgType { get; init; }

    public Role? OrgRole { get; init; }

    public bool? IsImpersonating { get; init; }

    public ImpersonationKind? ImpersonationKind { get; init; }

    public Guid? ImpersonatedBy { get; init; }

    public Guid? ImpersonationSessionId { get; init; }

    public Guid? ImpersonatorOrgId { get; init; }

    public string? ImpersonatorOrgName { get; init; }

    public OrgType? ImpersonatorOrgType { get; init; }

    public Role? ImpersonatorOrgRole { get; init; }

    public IReadOnlySet<string> Scopes { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}

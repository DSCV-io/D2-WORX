// -----------------------------------------------------------------------
// <copyright file="ScopeMatch.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Controls how the auth middleware evaluates the endpoint's declared scope
/// set against the caller's <c>IRequestContext.Scopes</c>. Attached to
/// <c>EndpointScopeMetadata</c> via the fluent
/// <c>RequireAnyScope</c> / <c>RequireAllScopes</c> extensions and consumed
/// by <c>JwtAuthMiddleware</c> during request dispatch.
/// </summary>
public enum ScopeMatch
{
    /// <summary>
    /// Caller must hold <b>at least one</b> of the declared scopes. An
    /// authenticated caller whose <c>IRequestContext.Scopes</c> set overlaps
    /// with the endpoint's scope set by one or more entries passes the check.
    /// Equivalent to the historical "any-of" behavior on
    /// <c>IAuthContextExtensions.HasAnyScope</c>.
    /// </summary>
    Any,

    /// <summary>
    /// Caller must hold <b>every</b> declared scope. An authenticated caller
    /// passes the check only when their <c>IRequestContext.Scopes</c> set is
    /// a superset of the endpoint's full scope set. Use when an operation
    /// requires joint capability (e.g., both read and write permission must
    /// be present simultaneously).
    /// </summary>
    All,
}

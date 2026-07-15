// -----------------------------------------------------------------------
// <copyright file="HandlerScopeMatch.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

/// <summary>
/// Controls how <c>BaseHandler</c> evaluates the per-handler scope requirement
/// declared via <see cref="ScopeRequirement"/> against the caller's
/// <c>IRequestContext.Scopes</c>. Mirrors the transport-layer
/// <c>DcsvIo.D2.Auth.Abstractions.ScopeMatch</c> enum but lives in the handler
/// layer so that handler code never takes a compile-time dependency on the auth
/// layer (layer-hygiene invariant).
/// </summary>
public enum HandlerScopeMatch
{
    /// <summary>
    /// Caller must hold <b>at least one</b> of the declared scopes. An
    /// authenticated caller whose <c>IRequestContext.Scopes</c> set overlaps
    /// with the handler's scope set by one or more entries passes the check.
    /// Use for operations that can be performed by holders of any one of
    /// several related permissions.
    /// </summary>
    Any,

    /// <summary>
    /// Caller must hold <b>every</b> declared scope. An authenticated caller
    /// passes the check only when their <c>IRequestContext.Scopes</c> set is
    /// a superset of the handler's full scope set. Use when an operation
    /// requires joint capability (e.g., both read and write permission must
    /// be present simultaneously).
    /// </summary>
    All,
}

// -----------------------------------------------------------------------
// <copyright file="IHandlerContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

using DcsvIo.D2.Context.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Per-request context surface that every handler receives. Provides typed
/// access to the read-only request context and to a logger pre-scoped to the
/// handler's identity. Implementations are registered Transient with DI so
/// each handler resolution gets a fresh, request-scoped instance.
/// </summary>
public interface IHandlerContext
{
    /// <summary>
    /// Gets the read-only request context (auth + transport + WhoIs +
    /// fingerprint).
    /// </summary>
    IRequestContext Request { get; }

    /// <summary>
    /// Gets the logger pre-scoped to the handler's category / source-context.
    /// </summary>
    ILogger Logger { get; }
}

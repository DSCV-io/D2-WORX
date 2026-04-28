// -----------------------------------------------------------------------
// <copyright file="IHandlerContext.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

using Microsoft.Extensions.Logging;

/// <summary>
/// Defines a context for handlers, providing access to the request context and logging
/// capabilities.
/// </summary>
public interface IHandlerContext
{
    /// <summary>
    /// Gets the request context associated with the handler.
    /// </summary>
    IRequestContext Request { get; }

    /// <summary>
    /// Gets the logger for logging within the handler.
    /// </summary>
    ILogger Logger { get; }
}

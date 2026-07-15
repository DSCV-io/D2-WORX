// -----------------------------------------------------------------------
// <copyright file="HandlerContext.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Handler.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Concrete <see cref="IHandlerContext"/> bound to the consuming handler's
/// type via the generic parameter. Open-generic registration in
/// <see cref="HandlerServiceCollectionExtensions.AddD2Handler"/> means each
/// handler resolution gets a <see cref="HandlerContext{T}"/> with the right
/// <see cref="ILogger{T}"/> source-context already in place.
/// </summary>
/// <typeparam name="T">
/// The handler type — used as the <see cref="ILogger{T}"/> category.
/// </typeparam>
public sealed class HandlerContext<T> : IHandlerContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerContext{T}"/>
    /// class.
    /// </summary>
    /// <param name="request">
    /// The read-only request context (resolved per-request via DI scope).
    /// </param>
    /// <param name="logger">
    /// The typed logger (auto-categorized to the handler's full type name).
    /// </param>
    public HandlerContext(IRequestContext request, ILogger<T> logger)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(logger);

        Request = request;
        Logger = logger;
    }

    /// <inheritdoc/>
    public IRequestContext Request { get; }

    /// <inheritdoc/>
    public ILogger Logger { get; }
}

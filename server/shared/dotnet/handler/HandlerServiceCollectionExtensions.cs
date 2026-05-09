// -----------------------------------------------------------------------
// <copyright file="HandlerServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI extensions for registering the handler stack.
/// </summary>
public static class HandlerServiceCollectionExtensions
{
    /// <summary>
    /// Registers the open-generic <see cref="HandlerContext{T}"/> as Transient.
    /// Idempotent — safe to call multiple times. Does NOT register
    /// <see cref="D2.Shared.Context.Abstractions.IRequestContext"/> —
    /// that's transport-specific (HTTP middleware via handler-aspnetcore,
    /// RabbitMQ consumer middleware via handler-messaging).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddD2Handler(this IServiceCollection services)
    {
        services.TryAdd(ServiceDescriptor.Transient(typeof(HandlerContext<>), typeof(HandlerContext<>)));
        return services;
    }
}

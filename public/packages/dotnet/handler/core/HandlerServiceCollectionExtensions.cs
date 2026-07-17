// -----------------------------------------------------------------------
// <copyright file="HandlerServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI extensions for registering the handler stack.
/// </summary>
public static class HandlerServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the open-generic <see cref="HandlerContext{T}"/> as Transient.
        /// Idempotent — safe to call multiple times. Does NOT register
        /// <see cref="DcsvIo.D2.Context.Abstractions.IRequestContext"/> —
        /// that's transport-specific (each consuming transport stack builds and
        /// scopes its own IRequestContext before any handler resolves: HTTP/gRPC
        /// via per-service AspNetCore middleware, RabbitMQ via consumer pipeline).
        /// </summary>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public IServiceCollection AddD2Handler()
        {
            services.TryAdd(ServiceDescriptor.Transient(
                typeof(HandlerContext<>), typeof(HandlerContext<>)));
            return services;
        }
    }
}

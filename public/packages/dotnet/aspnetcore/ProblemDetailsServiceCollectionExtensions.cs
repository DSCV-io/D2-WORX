// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore;

using DcsvIo.D2.AspNetCore.Internal;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// DI registration entry point for the D² <c>ProblemDetails</c> customizer
/// (RFC 7807 problem-details response enrichment).
/// </summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers ASP.NET Core's <c>IProblemDetailsService</c> via the
        /// framework's <c>AddProblemDetails</c> extension on
        /// <see cref="IServiceCollection"/> with the D² customizer applied as the
        /// <c>CustomizeProblemDetails</c> callback. The customizer adds
        /// <c>traceId</c> (from <see cref="System.Diagnostics.Activity.Current"/>
        /// falling back to
        /// <see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/>)
        /// + <c>correlationId</c> (from the configured request header,
        /// length-capped, or a freshly generated GUID echoed back via the
        /// response header) to the
        /// <c>ProblemDetails.Extensions</c> dictionary, and (when enabled)
        /// populates the RFC 7807 <c>instance</c> field with
        /// <c>{Method} {Path}</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// PII discipline: the customizer NEVER reads
        /// <c>HttpContext.Request.QueryString</c>, <c>Request.Body</c>, or
        /// any user-input source. The inbound correlation-id header value
        /// is length-capped at
        /// <see cref="D2AspNetCoreConstants.MAX_CORRELATION_ID_LENGTH"/>
        /// to prevent an arbitrary-length user header from inflating the
        /// response body — values exceeding the cap are treated as absent
        /// and a fresh GUID is generated.
        /// </para>
        /// <para>
        /// Calling <c>AddProblemDetails</c> twice on the same
        /// <see cref="IServiceCollection"/> stacks the customizers per the
        /// underlying ASP.NET Core convention; the LAST registered
        /// customizer wins on conflicting <c>Extensions</c> keys. This
        /// extension is idempotent at the options-binding level (the same
        /// <see cref="D2ProblemDetailsOptions"/> instance is reused).
        /// </para>
        /// </remarks>
        /// <param name="configure">
        /// Optional <see cref="D2ProblemDetailsOptions"/> customizer.
        /// </param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is null.
        /// </exception>
        public IServiceCollection AddD2ProblemDetails(
            Action<D2ProblemDetailsOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOptions<D2ProblemDetailsOptions>()
                .Configure(opts => configure?.Invoke(opts))
                .Validate(
                    o => o.CorrelationIdHeaderName.Truthy(),
                    "D2ProblemDetailsOptions.CorrelationIdHeaderName must "
                    + "not be empty / whitespace.")
                .ValidateOnStart();

            services.AddProblemDetails(opts =>
            {
                opts.CustomizeProblemDetails = ctx =>
                {
                    var d2Options = ctx.HttpContext.RequestServices
                        .GetRequiredService<IOptions<D2ProblemDetailsOptions>>()
                        .Value;
                    D2ProblemDetailsCustomizer.Apply(ctx, d2Options);
                };
            });

            return services;
        }
    }
}

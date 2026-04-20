// -----------------------------------------------------------------------
// <copyright file="ServiceKeyEndpointFilter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ServiceKey.Default;

using D2.Shared.Handler;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Endpoint filter that verifies the request has been identified as a trusted
/// service by the <see cref="ServiceKeyMiddleware"/>. Returns 401 Unauthorized
/// if the trust flag is not set.
/// </summary>
/// <remarks>
/// This filter no longer validates the key itself — the <see cref="ServiceKeyMiddleware"/>
/// handles that earlier in the pipeline. This filter simply checks the
/// <see cref="IRequestContext.IsTrustedService"/> flag.
/// </remarks>
public class ServiceKeyEndpointFilter : IEndpointFilter
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var requestContext = context.HttpContext.Features.Get<IRequestContext>();

        if (requestContext?.IsTrustedService != true)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: "A valid service API key is required.");
        }

        return await next(context);
    }
}

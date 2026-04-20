// -----------------------------------------------------------------------
// <copyright file="JwtAuthExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.JwtAuth.Default;

using D2.Shared.AuthPolicy.Default;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Extension methods for adding JWT authentication to the REST gateway.
/// </summary>
public static partial class JwtAuthExtensions
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds JWT Bearer authentication configured against the Auth Service JWKS endpoint.
        /// </summary>
        ///
        /// <param name="configuration">
        /// The application configuration.
        /// </param>
        /// <param name="sectionName">
        /// The configuration section name. Defaults to "GATEWAY_AUTH".
        /// </param>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddJwtAuth(
            IConfiguration configuration,
            string sectionName = "GATEWAY_AUTH")
        {
            var options = new JwtAuthOptions();
            configuration.GetSection(sectionName).Bind(options);

            services.Configure<JwtAuthOptions>(configuration.GetSection(sectionName));

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    // Preserve original JWT claim names (e.g., "sub", "orgId").
                    // Without this, ASP.NET remaps "sub" to the long
                    // ClaimTypes.NameIdentifier URI, breaking FindFirst("sub").
                    jwt.MapInboundClaims = false;

                    // JWKS endpoint for automatic key retrieval and rotation.
                    // Auth service is internal — never exposed to the internet.
                    // TLS termination is handled by the reverse proxy in production.
                    jwt.RequireHttpsMetadata = false;

                    // BetterAuth does not serve a standard OIDC discovery document.
                    // Use a custom retriever that fetches the raw JWKS endpoint and
                    // wraps it in an OpenIdConnectConfiguration for the framework.
                    var jwksUrl = $"{options.AuthServiceBaseUrl.TrimEnd('/')}/api/auth/jwks";
                    jwt.Configuration = null;
                    jwt.ConfigurationManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
                        jwksUrl,
                        new JwksConfigurationRetriever(options.Issuer),
                        new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever { RequireHttps = false })
                    {
                        AutomaticRefreshInterval = options.JwksAutoRefreshInterval,
                        RefreshInterval = options.JwksRefreshInterval,
                    };

                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = options.Issuer,
                        ValidateAudience = true,
                        ValidAudience = options.Audience,
                        ValidateLifetime = true,
                        ClockSkew = options.ClockSkew,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        ValidAlgorithms = ["RS256"],
                    };
                    jwt.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            // Log but don't expose failure details to client.
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("JwtAuth");
                            LogJwtAuthenticationFailed(logger, context.Exception, context.HttpContext.Request.Path.Value);
                            return Task.CompletedTask;
                        },
                    };
                });

            services.AddAuthorization(o => o.AddD2Policies());

            return services;
        }
    }

    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>.
    /// </summary>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds JWT authentication and authorization middleware to the pipeline.
        /// </summary>
        ///
        /// <returns>
        /// The application builder for chaining.
        /// </returns>
        /// <remarks>
        /// Must be placed after request enrichment and rate limiting,
        /// but before idempotency middleware.
        /// </remarks>
        public IApplicationBuilder UseJwtAuth()
        {
            app.UseAuthentication();
            app.UseMiddleware<JwtFingerprintMiddleware>();
            app.UseAuthorization();

            return app;
        }
    }

    /// <summary>
    /// Logs that JWT authentication failed for a request path.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "JWT authentication failed for {Path}")]
    private static partial void LogJwtAuthenticationFailed(ILogger logger, Exception ex, string? path);
}

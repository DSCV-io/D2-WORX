// -----------------------------------------------------------------------
// <copyright file="MutualTlsHostExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AspNetCore.Mtls;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Host wiring for mutual-TLS client-certificate require-and-validate. When
/// <see cref="D2MutualTlsOptions.Enabled"/>, Kestrel's HTTPS endpoint is configured
/// to REQUIRE a client certificate and to validate it with the default-deny
/// <see cref="SpiffeSanPeerValidator"/>. The Kestrel-config LOGIC lives here in
/// <c>DcsvIo.D2.AspNetCore</c>; the service-defaults aggregator composes it.
/// </summary>
public static class MutualTlsHostExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Wires mutual-TLS client-certificate require-and-validate into the host's
        /// Kestrel HTTPS endpoint. When <see cref="D2MutualTlsOptions.Enabled"/>,
        /// every HTTPS connection MUST present a client certificate that the
        /// default-deny <see cref="SpiffeSanPeerValidator"/> accepts (chains to the
        /// internal CA, SPIFFE trust domain matches, workload in the allowed set).
        /// When disabled, no Kestrel client-certificate configuration is added.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Fail-loud, not fail-open.</b> The options are validated at host build
        /// via <c>ValidateOnStart()</c>: when <see cref="D2MutualTlsOptions.Enabled"/>
        /// is true, <see cref="D2MutualTlsOptions.AllowedWorkloads"/> MUST be
        /// non-empty AND <see cref="D2MutualTlsOptions.TrustAnchorsProvider"/> MUST
        /// be set, or the host throws rather than silently accepting (or rejecting)
        /// every peer.
        /// </para>
        /// <para>
        /// <b>Connection-level concern.</b> Client-certificate validation fires at
        /// the TLS handshake before any middleware, so the wiring is entirely
        /// service-collection-phase via <see cref="KestrelServerOptions"/> — there
        /// is no <c>IApplicationBuilder</c> pipeline step.
        /// </para>
        /// <para>
        /// <b>Compose, don't clobber.</b> The validation callback is set through an
        /// <see cref="IConfigureOptions{TOptions}"/> for <see cref="KestrelServerOptions"/>
        /// so it composes with the host's other HTTPS configuration; it never calls
        /// <c>AllowAnyClientCertificate()</c> (which would discard the callback).
        /// </para>
        /// </remarks>
        /// <param name="configure">Configuration delegate for <see cref="D2MutualTlsOptions"/>.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
        /// </exception>
        public IServiceCollection AddD2MutualTls(Action<D2MutualTlsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<D2MutualTlsOptions>()
                .Configure(configure)
                .Validate(
                    o => !o.Enabled || o.AllowedWorkloads.Truthy(),
                    "D2MutualTlsOptions.AllowedWorkloads must contain at least one "
                    + "workload when Enabled is true (a require-certificate host that "
                    + "allows no workload accepts nobody — fail-loud config error).")
                .Validate(
                    o => !o.Enabled || o.AllowedWorkloads.All(w => w.Truthy()),
                    "D2MutualTlsOptions.AllowedWorkloads entries must not be empty / "
                    + "whitespace.")
                .Validate(
                    o => !o.Enabled || o.TrustAnchorsProvider is not null,
                    "D2MutualTlsOptions.TrustAnchorsProvider must be set when Enabled "
                    + "is true (the host supplies the public internal CA trust anchor "
                    + "the peer certificate chains to).")
                .ValidateOnStart();

            // The validator is resolved per validation from the options snapshot.
            // Singleton — stateless given the resolved options.
            services.TryAddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<D2MutualTlsOptions>>().Value;
                var logger = sp.GetRequiredService<
                    Microsoft.Extensions.Logging.ILogger<SpiffeSanPeerValidator>>();

                return new SpiffeSanPeerValidator(options, logger);
            });

            // Configure Kestrel's HTTPS default at options-build time via an
            // IConfigureOptions so the validator + options resolve from DI. The
            // callback only requires + validates a client cert when Enabled, so a
            // disabled host's HTTPS endpoint is untouched (safe-by-default).
            services.AddSingleton<IConfigureOptions<KestrelServerOptions>, MutualTlsKestrelConfigure>();

            return services;
        }
    }

    /// <summary>
    /// Configures Kestrel's HTTPS defaults to require + validate a client
    /// certificate when mutual-TLS is enabled. Registered against
    /// <see cref="IConfigureOptions{TOptions}"/> for <see cref="KestrelServerOptions"/>
    /// so the resolved <see cref="D2MutualTlsOptions"/> + <see cref="SpiffeSanPeerValidator"/>
    /// are available at configure time.
    /// </summary>
    private sealed class MutualTlsKestrelConfigure : IConfigureOptions<KestrelServerOptions>
    {
        private readonly IOptions<D2MutualTlsOptions> r_options;
        private readonly SpiffeSanPeerValidator r_validator;

        public MutualTlsKestrelConfigure(
            IOptions<D2MutualTlsOptions> options, SpiffeSanPeerValidator validator)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(validator);

            r_options = options;
            r_validator = validator;
        }

        public void Configure(KestrelServerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!r_options.Value.Enabled)
                return;

            options.ConfigureHttpsDefaults(https =>
            {
                // RequireCertificate so a no-certificate connection is rejected at
                // the handshake — the peer MUST present a client certificate.
                https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;

                // Default-deny: the callback returns true ONLY when the validator's
                // result is Ok. The cert + chain are borrowed from Kestrel — never
                // disposed here. The chain carries the peer-presented intermediate
                // the validator seeds into its root-anchored rebuild. NEVER
                // AllowAnyClientCertificate (it discards this callback).
                // SslPolicyErrors is intentionally NOT consulted — the validator
                // rebuilds the chain against OUR configured trust anchors with
                // CustomRootTrust, which supersedes the machine-store result.
                // A future maintainer must NOT restore the default-policy check.
                https.ClientCertificateValidation =
                    (cert, chain, _) => r_validator.Validate(cert, chain).Success;
            });
        }
    }
}

// -----------------------------------------------------------------------
// <copyright file="I18nServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n;

using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration for <see cref="ITranslator"/> + <see cref="SupportedLocales"/>.
/// </summary>
public static class I18nServiceCollectionExtensions
{
    /// <param name="services">The service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="SupportedLocales"/> + <see cref="ITranslator"/> as
        /// process-wide singletons. Both are constructed eagerly on first resolve;
        /// the <see cref="Translator"/> loads every JSON catalog from
        /// <paramref name="messagesDirectory"/> at construction.
        /// </summary>
        /// <param name="configuration">
        /// The application configuration. <c>PUBLIC_DEFAULT_LOCALE</c> + the
        /// indexed <c>PUBLIC_ENABLED_LOCALES</c> section drive
        /// <see cref="SupportedLocales"/> construction.
        /// </param>
        /// <param name="messagesDirectory">
        /// Optional override for the messages directory. Defaults to
        /// <c>{AppContext.BaseDirectory}/messages</c> — populated at build time
        /// via the consuming csproj's
        /// <c>&lt;Content Include="...contracts/messages/*.json" /&gt;</c> item group.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddD2I18n(
            IConfiguration configuration,
            string? messagesDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var resolvedDirectory = messagesDirectory ??
                Path.Combine(AppContext.BaseDirectory, DEFAULT_MESSAGES_DIRECTORY_NAME);

            services.TryAddSingleton(_ => new SupportedLocales(configuration));
            services.TryAddSingleton<ITranslator>(sp =>
                new Translator(sp.GetRequiredService<SupportedLocales>(), resolvedDirectory));

            return services;
        }
    }

    private const string DEFAULT_MESSAGES_DIRECTORY_NAME = "messages";
}

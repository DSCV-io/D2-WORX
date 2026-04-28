// -----------------------------------------------------------------------
// <copyright file="LanguageSeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding language data.
/// </summary>
public static class LanguageSeeding
{
    /// <summary>
    /// Seeds the Language entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the Language entity.
        /// </summary>
        public void SeedLanguages()
        {
            modelBuilder.Entity<Language>().HasData(
                [

                    // English
                    new Language
                    {
                        ISO6391Code = "en",
                        Name = "English",
                        Endonym = "English",
                    },

                    // French
                    new Language
                    {
                        ISO6391Code = "fr",
                        Name = "French",
                        Endonym = "Français",
                    },

                    // Spanish
                    new Language
                    {
                        ISO6391Code = "es",
                        Name = "Spanish",
                        Endonym = "Español",
                    },

                    // German
                    new Language
                    {
                        ISO6391Code = "de",
                        Name = "German",
                        Endonym = "Deutsch",
                    },

                    // Italian
                    new Language
                    {
                        ISO6391Code = "it",
                        Name = "Italian",
                        Endonym = "Italiano",
                    },

                    // Japanese
                    new Language
                    {
                        ISO6391Code = "ja",
                        Name = "Japanese",
                        Endonym = "日本語",
                    }
                ]);
        }
    }
}

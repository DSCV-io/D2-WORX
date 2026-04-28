// -----------------------------------------------------------------------
// <copyright file="GeopoliticalEntitySeeding.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Infra.Repository.Seeding;

using D2.Geo.Domain.Entities;
using D2.Geo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Extension methods for seeding geopolitical entities.
/// </summary>
public static class GeopoliticalEntitySeeding
{
    /// <summary>
    /// Seeds the GeopoliticalEntity entity.
    /// </summary>
    ///
    /// <param name="modelBuilder">
    /// The model builder to configure the entity model.
    /// </param>
    extension(ModelBuilder modelBuilder)
    {
        /// <summary>
        /// Seeds the GeopoliticalEntity entity.
        /// </summary>
        public void SeedGeopoliticalEntities()
        {
            modelBuilder.Entity<GeopoliticalEntity>().HasData([

                // =====================================================================
                // Continents
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "AF",
                    Name = "Africa",
                    Type = GeopoliticalEntityType.Continent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "AN",
                    Name = "Antarctica",
                    Type = GeopoliticalEntityType.Continent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "AS",
                    Name = "Asia",
                    Type = GeopoliticalEntityType.Continent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "EU",
                    Name = "Europe",
                    Type = GeopoliticalEntityType.Continent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "NA",
                    Name = "North America",
                    Type = GeopoliticalEntityType.Continent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "OC",
                    Name = "Oceania",
                    Type = GeopoliticalEntityType.Continent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SA",
                    Name = "South America",
                    Type = GeopoliticalEntityType.Continent,
                },

                // =====================================================================
                // Subcontinents
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "ARAB",
                    Name = "Arabian Peninsula",
                    Type = GeopoliticalEntityType.SubContinent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CAM",
                    Name = "Central America",
                    Type = GeopoliticalEntityType.SubContinent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CAS",
                    Name = "Central Asia",
                    Type = GeopoliticalEntityType.SubContinent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "EAS",
                    Name = "East Asia",
                    Type = GeopoliticalEntityType.SubContinent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "INDS",
                    Name = "Indian Subcontinent",
                    Type = GeopoliticalEntityType.SubContinent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SCAN",
                    Name = "Scandinavia",
                    Type = GeopoliticalEntityType.SubContinent,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SEA",
                    Name = "Southeast Asia",
                    Type = GeopoliticalEntityType.SubContinent,
                },

                // =====================================================================
                // Geopolitical Regions
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "BALK",
                    Name = "Balkans",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "BALT",
                    Name = "Baltic States",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "BENE",
                    Name = "Benelux",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CARIB",
                    Name = "Caribbean",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "LATAM",
                    Name = "Latin America",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "MENA",
                    Name = "Middle East and North Africa",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "NORD",
                    Name = "Nordic Countries",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SAHEL",
                    Name = "Sahel",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SSA",
                    Name = "Sub-Saharan Africa",
                    Type = GeopoliticalEntityType.GeopoliticalRegion,
                },

                // =====================================================================
                // Free Trade Agreements
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "AFTA",
                    Name = "ASEAN Free Trade Area",
                    Type = GeopoliticalEntityType.FreeTradeAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CPTPP",
                    Name = "Comprehensive and Progressive Agreement for Trans-Pacific Partnership",
                    Type = GeopoliticalEntityType.FreeTradeAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "RCEP",
                    Name = "Regional Comprehensive Economic Partnership",
                    Type = GeopoliticalEntityType.FreeTradeAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "USMCA",
                    Name = "United States-Mexico-Canada Agreement",
                    Type = GeopoliticalEntityType.FreeTradeAgreement,
                },

                // =====================================================================
                // Customs Unions
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "EUCU",
                    Name = "European Union Customs Union",
                    Type = GeopoliticalEntityType.CustomsUnion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SACU",
                    Name = "Southern African Customs Union",
                    Type = GeopoliticalEntityType.CustomsUnion,
                },

                // =====================================================================
                // Common Markets
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "EEA",
                    Name = "European Economic Area",
                    Type = GeopoliticalEntityType.CommonMarket,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "MERCOSUR",
                    Name = "Southern Common Market (Mercosur)",
                    Type = GeopoliticalEntityType.CommonMarket,
                },

                // =====================================================================
                // Economic Unions
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "EAEU",
                    Name = "Eurasian Economic Union",
                    Type = GeopoliticalEntityType.EconomicUnion,
                },

                // =====================================================================
                // Monetary Unions
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "EZ",
                    Name = "Eurozone",
                    Type = GeopoliticalEntityType.MonetaryUnion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "ECCU",
                    Name = "Eastern Caribbean Currency Union",
                    Type = GeopoliticalEntityType.MonetaryUnion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "WAEMU",
                    Name = "West African Economic and Monetary Union",
                    Type = GeopoliticalEntityType.MonetaryUnion,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CEMAC",
                    Name = "Central African Economic and Monetary Community",
                    Type = GeopoliticalEntityType.MonetaryUnion,
                },

                // =====================================================================
                // Political Unions
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "EUR",
                    Name = "European Union",
                    Type = GeopoliticalEntityType.PoliticalUnion,
                },

                // =====================================================================
                // Governance and Cooperation Agreements
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "AU",
                    Name = "African Union",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "AL",
                    Name = "Arab League",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "ASEAN",
                    Name = "Association of Southeast Asian Nations",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "BRICS",
                    Name = "BRICS",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CARICOM",
                    Name = "Caribbean Community",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "COE",
                    Name = "Council of Europe",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CW",
                    Name = "Commonwealth of Nations",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "G7",
                    Name = "Group of Seven",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "G20",
                    Name = "Group of Twenty",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "GCC",
                    Name = "Gulf Cooperation Council",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "NC",
                    Name = "Nordic Council",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "OECD",
                    Name = "Organisation for Economic Co-operation and Development",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "OIF",
                    Name = "Organisation internationale de la Francophonie",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "OPEC",
                    Name = "Organization of the Petroleum Exporting Countries",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "SAARC",
                    Name = "South Asian Association for Regional Cooperation",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "UN",
                    Name = "United Nations",
                    Type = GeopoliticalEntityType.GovernanceAndCooperationAgreement,
                },

                // =====================================================================
                // Military Alliances
                // =====================================================================
                new GeopoliticalEntity
                {
                    ShortCode = "ANZUS",
                    Name = "Australia, New Zealand, United States Security Treaty",
                    Type = GeopoliticalEntityType.MilitaryAlliance,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "AUKUS",
                    Name = "Australia-United Kingdom-United States",
                    Type = GeopoliticalEntityType.MilitaryAlliance,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "CSTO",
                    Name = "Collective Security Treaty Organization",
                    Type = GeopoliticalEntityType.MilitaryAlliance,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "FVEY",
                    Name = "Five Eyes",
                    Type = GeopoliticalEntityType.MilitaryAlliance,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "NATO",
                    Name = "North Atlantic Treaty Organization",
                    Type = GeopoliticalEntityType.MilitaryAlliance,
                },
                new GeopoliticalEntity
                {
                    ShortCode = "QUAD",
                    Name = "Quadrilateral Security Dialogue",
                    Type = GeopoliticalEntityType.MilitaryAlliance,
                }
            ]);
        }
    }
}

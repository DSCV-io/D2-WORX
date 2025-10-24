namespace Geo.Domain.Enums;

public enum GeopoliticalEntityType
{
    #region General Geopolitical Regions
    /// <summary>
    /// A continent.
    /// </summary>
    /// <example>
    /// North America, Europe, Asia.
    /// </example>
    Continent,

    /// <summary>
    /// A sub-continent.
    /// </summary>
    /// <example>
    /// Indian Subcontinent, Arabia, Scandinavia, Central America.
    /// </example>
    SubContinent,

    /// <summary>
    /// A geopolitical region.
    /// </summary>
    /// <example>
    /// Middle East, Latin America, Caribbean.
    /// </example>
    GeopoliticalRegion,
    #endregion

    #region Economic Agreements / Unions
    /// <summary>
    /// Agreements that reduce or eliminate tariffs, import quotas, and
    /// preferences on goods and services traded between member countries.
    /// </summary>
    /// <example>
    /// United States-Mexico-Canada Agreement (USMCA),
    /// ASEAN Free Trade Area (AFTA).
    /// </example>
    FreeTradeAgreement,

    /// <summary>
    /// Agreements where member countries remove trade barriers among themselves
    /// and adopt a common external tariff toward non-member countries
    /// </summary>
    /// <example>
    /// European Union Customs Union (EUCU),
    /// Southern African Customs Union (SACU).
    /// </example>
    CustomsUnion,

    /// <summary>
    /// Similar to customs unions but with the free movement of factors of
    /// production like labor and capital (e.g., the European Economic Area).
    /// </summary>
    /// <example>
    /// The Southern Common Market (Mercosur / Mercosul),
    /// European Economic Area (EEA).
    /// </example>
    CommonMarket,

    /// <summary>
    ///  Integration involving harmonization of economic policies, including a
    /// common currency or fiscal policies.
    /// </summary>
    /// <example>
    /// Eurasian Economic Union (EAEU).
    /// </example>
    /// <remarks>
    /// While the European Union is often considered an economic union, it is
    /// also a political union.
    /// </remarks>
    EconomicUnion,

    /// <summary>
    /// Integration where member states adopt a single currency or closely
    /// coordinate monetary policies.
    /// </summary>
    /// <example>
    /// Eurozone (EZ), Eastern Caribbean Currency Union (ECCU).
    /// </example>
    MonetaryUnion,

    /// <summary>
    /// Agreements to promote and protect investments between two or more
    /// countries.
    /// </summary>
    /// <example>
    /// The [United States] Argentina Bilateral Investment Treaty.
    /// </example>
    BilateralInvestmentTreaty,

    /// <summary>
    /// Agreements aimed at economic development and assistance, often including
    /// infrastructure projects or economic aid.
    /// </summary>
    /// <example>
    /// Cotonou Agreement.
    /// </example>
    DevelopmentAgreement,

    /// <summary>
    /// Agreements governing the shared use of natural resources like water,
    /// energy, or minerals.
    /// </summary>
    /// <example>
    /// Nile Basin Initiative.
    /// </example>
    ResourceSharingAgreement,
    #endregion

    #region Political Agreements / Unions
    /// <summary>
    /// Integration where member states have a common political structure,
    /// possibly leading toward a unified government.
    /// </summary>
    /// <example>
    /// European Union (EU).
    /// </example>
    PoliticalUnion,

    /// <summary>
    /// Treaties focused on protecting and promoting human rights.
    /// </summary>
    /// <example>
    /// European Convention on Human Rights (ECHR).
    /// </example>
    HumanRightsAgreement,

    /// <summary>
    /// Treaties focused on addressing environmental issues like climate change
    /// or biodiversity.
    /// </summary>
    /// <example>
    /// Paris Agreement.
    /// </example>
    EnvironmentalAgreement,

    /// <summary>
    /// Agreements focused on joint governance or cooperation on various
    /// political matters.
    /// </summary>
    /// <example>
    /// African Union (AU).
    /// </example>
    GovernanceAndCooperationAgreement,

    /// <summary>
    /// Agreements ending hostilities and outlining conditions for peace between
    /// warring states.
    /// </summary>
    /// <example>
    /// Treaty of Versailles.
    /// </example>
    PeaceTreaty,

    /// <summary>
    /// Agreements encouraging democratic governance and political reform.
    /// </summary>
    /// <example>
    /// European Neighbourhood Policy (ENP).
    /// </example>
    DemocracyPromotionAgreement,
    #endregion

    #region Military Agreements / Unions
    /// <summary>
    /// Agreements where member states pledge mutual defense and strategic
    /// military cooperation.
    /// </summary>
    /// <example>
    /// North Atlantic Treaty Organization (NATO).
    /// </example>
    MilitaryAlliance,

    /// <summary>
    /// Treaties to limit or reduce the proliferation and deployment of weapons.
    /// </summary>
    /// <example>
    /// Nuclear Nonproliferation Treaty (NPT).
    /// </example>
    ArmsControlAgreement,

    /// <summary>
    /// Agreements that define the legal status of military personnel in another
    /// country (SOFA).
    /// </summary>
    /// <example>
    /// U.S.–South Korea Status of Forces Agreement.
    /// </example>
    StatusOfForcesAgreement,

    /// <summary>
    /// Agreements authorizing or organizing multinational peacekeeping
    /// operations.
    /// </summary>
    /// <example>
    /// The Kosovo Force (KFOR).
    /// </example>
    PeacekeepingAgreement,

    /// <summary>
    /// Agreements focusing on joint military training, resource sharing, or
    /// defense technology development.
    /// </summary>
    /// <example>
    /// Canada-Ukraine Strategic Security Partnership (CUSSP).
    /// </example>
    SecurityCooperationAgreement,

    /// <summary>
    /// Agreements in which countries pledge not to engage in military action
    /// against each other.
    /// </summary>
    /// <example>
    /// Molotov-Ribbentrop Pact.
    /// </example>
    NonAggressionPact,
    #endregion
}

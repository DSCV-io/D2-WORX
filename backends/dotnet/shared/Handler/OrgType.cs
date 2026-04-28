// -----------------------------------------------------------------------
// <copyright file="OrgType.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

/// <summary>
/// Represents the type of organization.
/// </summary>
public enum OrgType
{
    /// <summary>
    /// An administrative organization has full access to manage and configure the system.
    /// </summary>
    Admin,

    /// <summary>
    /// A support organization has basic administrative capabilities, typically for customer
    /// support purposes.
    /// </summary>
    Support,

    /// <summary>
    /// Affiliate organizations are partners or resellers associated with the system.
    /// </summary>
    Affiliate,

    /// <summary>
    /// A customer organization is a collection of standard users who utilize the system's
    /// services.
    /// </summary>
    Customer,

    /// <summary>
    /// A third-party organization represents an external client or partner associated
    /// with a customer organization. More distinct naming than "CustomerClient" to
    /// avoid confusion with the "Customer" type.
    /// </summary>
    ThirdParty,
}

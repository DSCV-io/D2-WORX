// -----------------------------------------------------------------------
// <copyright file="OrgType.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Organization type discriminator. Stored as a lowercase string in the
/// <c>d2_org_type</c> JWT claim and in the database; mapped to / from the enum via
/// reflection-friendly parse helpers.
/// </summary>
public enum OrgType
{
    /// <summary>
    /// Administrative organization — full system access for the platform operator.
    /// </summary>
    Admin,

    /// <summary>
    /// Support organization — basic administrative capabilities for customer-support staff.
    /// </summary>
    Support,

    /// <summary>
    /// Customer organization — standard end-user organization that consumes the platform's
    /// services.
    /// </summary>
    Customer,

    /// <summary>
    /// Third-party organization — external client / partner associated with a customer
    /// organization. More distinct naming than "CustomerClient" to avoid confusion with
    /// <see cref="Customer"/>.
    /// </summary>
    ThirdParty,

    /// <summary>
    /// Affiliate organization — partner / reseller associated with the platform.
    /// </summary>
    Affiliate,
}

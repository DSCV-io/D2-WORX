// -----------------------------------------------------------------------
// <copyright file="ContactVoDecorator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.EntityFrameworkCore;

using System;
using System.Security.Cryptography;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Internal single-source per-VO decoration core consumed by the per-VO
/// <c>MapXxx</c> complex-type helpers in <see cref="ContactMappingExtensions"/>.
/// Changing a field rule here updates the helper for every host that uses it.
/// </summary>
/// <remarks>
/// Per-field anonymize defaults are written via the
/// <c>DcsvIo.D2.DataGovernance.EntityFrameworkCore</c> fluent <c>.Anonymize*</c> API
/// on <c>ComplexTypePropertyBuilder&lt;T&gt;</c> (returned by <c>cp.Property(lambda)</c>).
/// </remarks>
internal static class ContactVoDecorator
{
    // =========================================================================
    // Private constants — caps not in FieldConstraints
    // =========================================================================

    // "v1." (3 chars) + SHA-256 hex (SHA256.HashSizeInBytes * 2 = 64 chars) = 67.
    private const int _HASH_ID_MAX = 3 + (SHA256.HashSizeInBytes * 2);

    // Cleared HashId sentinel written on erasure: "v1." + 64 × '0'.
    private static readonly string sr_hashIdCleared =
        "v1." + new string('0', SHA256.HashSizeInBytes * 2);

    // =========================================================================
    // Exposed constants for test assertions
    // =========================================================================

    /// <summary>Gets the HashId cleared sentinel used on erasure.</summary>
    internal static string HashIdCleared => sr_hashIdCleared;

    // =========================================================================
    // ComplexProperty-shape decorators
    // =========================================================================

    /// <summary>
    /// Decorates a <c>Personal</c> VO mapped as a <c>ComplexProperty</c>.
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>Personal</c>.</param>
    internal static void DecoratePersonal(ComplexPropertyBuilder<Personal> cp)
    {
        cp.Property(p => p.FirstName).HasMaxLength(FieldConstraints.FIRST_NAME_MAX)
          .Anonymize("Deleted");
        cp.Property(p => p.MiddleName).HasMaxLength(FieldConstraints.MIDDLE_NAME_MAX)
          .AnonymizeNull();
        cp.Property(p => p.LastName).HasMaxLength(FieldConstraints.LAST_NAME_MAX)
          .AnonymizeNull();
        cp.Property(p => p.PreferredName).HasMaxLength(FieldConstraints.PREFERRED_NAME_MAX)
          .AnonymizeNull();
        cp.Property(p => p.HashId).HasMaxLength(_HASH_ID_MAX)
          .Anonymize(sr_hashIdCleared);
    }

    /// <summary>
    /// Decorates a <c>NameAffixes</c> VO mapped as a <c>ComplexProperty</c>.
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>NameAffixes</c>.</param>
    internal static void DecorateAffixes(ComplexPropertyBuilder<NameAffixes> cp)
    {
        cp.Property(a => a.Prefix).AnonymizeNull();
        cp.Property(a => a.PrefixCustom).HasMaxLength(FieldConstraints.AFFIX_CUSTOM_MAX)
          .AnonymizeNull();
        cp.Property(a => a.Suffix).AnonymizeNull();
        cp.Property(a => a.SuffixCustom).HasMaxLength(FieldConstraints.AFFIX_CUSTOM_MAX)
          .AnonymizeNull();
    }

    /// <summary>
    /// Decorates a <c>Demographics</c> VO mapped as a <c>ComplexProperty</c>.
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>Demographics</c>.</param>
    internal static void DecorateDemographics(ComplexPropertyBuilder<Demographics> cp)
    {
        cp.Property(d => d.DateOfBirth).AnonymizeNull();
        cp.Property(d => d.BiologicalSex).AnonymizeNull();
    }

    /// <summary>
    /// Decorates a <c>Professional</c> VO mapped as a <c>ComplexProperty</c>.
    /// The <c>CompanyWebsite</c> <see cref="Uri"/> field gets a
    /// <c>string ↔ AbsoluteUri</c> value converter plus <c>HasMaxLength</c>.
    /// </summary>
    /// <param name="cp">The complex-property builder for <c>Professional</c>.</param>
    internal static void DecorateProfessional(ComplexPropertyBuilder<Professional> cp)
    {
        cp.Property(p => p.CompanyName).HasMaxLength(FieldConstraints.COMPANY_NAME_MAX)
          .Anonymize("Deleted");
        cp.Property(p => p.JobTitle).HasMaxLength(FieldConstraints.JOB_TITLE_MAX)
          .AnonymizeNull();
        cp.Property(p => p.Department).HasMaxLength(FieldConstraints.DEPARTMENT_MAX)
          .AnonymizeNull();
        cp.Property(p => p.CompanyWebsite)
          .AnonymizeNull()
          .HasConversion(
              u => u == null ? null : u.AbsoluteUri,
              s => s == null ? null : new Uri(s, UriKind.Absolute))
          .HasMaxLength(FieldConstraints.COMPANY_WEBSITE_MAX);
    }
}

// -----------------------------------------------------------------------
// <copyright file="ContactMappingExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Contacts.EntityFrameworkCore;

using DcsvIo.D2.Contacts.ValueObjects;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Per-VO complex-type mapping helpers for the DcsvIo.D2.Contacts value objects. Called
/// from the host's <c>IEntityTypeConfiguration&lt;T&gt;</c> inside a
/// <c>b.ComplexProperty</c> callback. Each helper: wires <c>HasMaxLength</c> from
/// <c>FieldConstraints.*</c>, applies any necessary value converters (Uri), and writes the
/// per-field anonymize defaults via the fluent <c>.Anonymize*</c> API.
/// </summary>
/// <remarks>
/// <para>
/// The host keeps the domain aggregate completely free of EF references — a VO-typed
/// property on a host entity is a plain CLR property; all mapping lives in the infra
/// <c>IEntityTypeConfiguration&lt;T&gt;</c> class.
/// </para>
/// <para>
/// <b>Same-VO-type-twice</b> (e.g. legal name + maiden name <c>Personal</c>): call the
/// helper twice via two distinct host-property selectors. EF Core 10 prefixes complex
/// columns by the owning-property path automatically, producing distinct column sets
/// (<c>LegalName_FirstName</c> vs <c>MaidenName_FirstName</c>). The helpers never call
/// <c>HasColumnName</c>, which preserves this default uniquification.
/// </para>
/// </remarks>
public static class ContactMappingExtensions
{
    // =========================================================================
    // ComplexPropertyBuilder<Personal>
    // =========================================================================
    extension(ComplexPropertyBuilder<Personal> builder)
    {
        /// <summary>
        /// Configures a <c>Personal</c> complex-property column set: wires
        /// <c>HasMaxLength</c> on all name fields and <c>HashId</c>, and writes
        /// the per-field anonymize defaults.
        /// Anonymize defaults: FirstName → <c>"Deleted"</c> (constant);
        /// Middle/Last/Preferred → SetNull; HashId → cleared sentinel.
        /// </summary>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<Personal> MapPersonal()
        {
            ContactVoDecorator.DecoratePersonal(builder);
            return builder;
        }
    }

    // =========================================================================
    // ComplexPropertyBuilder<NameAffixes>
    // =========================================================================
    extension(ComplexPropertyBuilder<NameAffixes> builder)
    {
        /// <summary>
        /// Configures a <c>NameAffixes</c> complex-property column set: wires
        /// <c>HasMaxLength</c> on custom-affix fields and writes SetNull anonymize
        /// defaults for all four fields.
        /// </summary>
        /// <remarks>
        /// <c>NameAffixes</c> has no required scalar property. EF Core requires a complex
        /// type to be REQUIRED (non-nullable) unless it has at least one required property,
        /// so the host entity must declare its <c>NameAffixes</c> member as non-nullable,
        /// or EF throws at model build.
        /// </remarks>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<NameAffixes> MapNameAffixes()
        {
            ContactVoDecorator.DecorateAffixes(builder);
            return builder;
        }
    }

    // =========================================================================
    // ComplexPropertyBuilder<Demographics>
    // =========================================================================
    extension(ComplexPropertyBuilder<Demographics> builder)
    {
        /// <summary>
        /// Configures a <c>Demographics</c> complex-property column set: writes
        /// SetNull anonymize defaults for <c>DateOfBirth</c> and
        /// <c>BiologicalSex</c> (no length facets — date/enum fields).
        /// </summary>
        /// <remarks>
        /// <c>Demographics</c> has no required scalar property. EF Core requires a complex
        /// type to be REQUIRED (non-nullable) unless it has at least one required property,
        /// so the host entity must declare its <c>Demographics</c> member as non-nullable,
        /// or EF throws at model build.
        /// </remarks>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<Demographics> MapDemographics()
        {
            ContactVoDecorator.DecorateDemographics(builder);
            return builder;
        }
    }

    // =========================================================================
    // ComplexPropertyBuilder<Professional>
    // =========================================================================
    extension(ComplexPropertyBuilder<Professional> builder)
    {
        /// <summary>
        /// Configures a <c>Professional</c> complex-property column set: wires
        /// <c>HasMaxLength</c>, the <c>Uri ↔ AbsoluteUri</c> value converter on
        /// <c>CompanyWebsite</c>, and the per-field anonymize defaults.
        /// Anonymize defaults: CompanyName → <c>"Deleted"</c>; Job/Dept/Website → SetNull.
        /// </summary>
        /// <returns>The same builder for fluent chaining.</returns>
        public ComplexPropertyBuilder<Professional> MapProfessional()
        {
            ContactVoDecorator.DecorateProfessional(builder);
            return builder;
        }
    }
}

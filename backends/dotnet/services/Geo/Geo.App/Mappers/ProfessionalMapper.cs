// -----------------------------------------------------------------------
// <copyright file="ProfessionalMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Mapper for converting between <see cref="Professional"/> and <see cref="ProfessionalDTO"/>.
/// </summary>
public static class ProfessionalMapper
{
    /// <summary>
    /// Maps <see cref="Professional"/> to <see cref="ProfessionalDTO"/>.
    /// </summary>
    ///
    /// <param name="professional">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(Professional professional)
    {
        /// <summary>
        /// Converts the <see cref="Professional"/> domain object to a <see cref="ProfessionalDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="ProfessionalDTO"/> object.
        /// </returns>
        public ProfessionalDTO ToDTO()
        {
            var dto = new ProfessionalDTO
            {
                CompanyName = professional.CompanyName,
            };

            // Optional fields — only set when non-null to avoid proto CheckNotNull.
            if (professional.JobTitle != null)
            {
                dto.JobTitle = professional.JobTitle;
            }

            if (professional.Department != null)
            {
                dto.Department = professional.Department;
            }

            if (professional.CompanyWebsite != null)
            {
                dto.CompanyWebsite = professional.CompanyWebsite.ToString();
            }

            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="ProfessionalDTO"/> to <see cref="Professional"/>.
    /// </summary>
    ///
    /// <param name="professionalDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(ProfessionalDTO professionalDTO)
    {
        /// <summary>
        /// Converts the <see cref="ProfessionalDTO"/> to a <see cref="Professional"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="Professional"/> domain object.
        /// </returns>
        public Professional ToDomain()
        {
            return Professional.Create(
                professionalDTO.CompanyName,
                professionalDTO.JobTitle.ToNullIfEmpty(),
                professionalDTO.Department.ToNullIfEmpty(),
                Uri.TryCreate(professionalDTO.CompanyWebsite.ToNullIfEmpty(), UriKind.Absolute, out var uri) ? uri : null);
        }
    }
}

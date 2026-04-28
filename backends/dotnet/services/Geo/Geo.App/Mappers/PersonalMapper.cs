// -----------------------------------------------------------------------
// <copyright file="PersonalMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using System.Globalization;
using D2.Geo.Domain.Enums;
using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Mapper for converting between <see cref="Personal"/> and <see cref="PersonalDTO"/>.
/// </summary>
public static class PersonalMapper
{
    /// <summary>
    /// Maps <see cref="Personal"/> to <see cref="PersonalDTO"/>.
    /// </summary>
    ///
    /// <param name="personal">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(Personal personal)
    {
        /// <summary>
        /// Converts the <see cref="Personal"/> domain object to a <see cref="PersonalDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="PersonalDTO"/> object.
        /// </returns>
        public PersonalDTO ToDTO()
        {
            var dto = new PersonalDTO
            {
                FirstName = personal.FirstName,
            };

            // Optional fields — only set when non-null to avoid proto CheckNotNull.
            if (personal.Title != null)
            {
                dto.Title = personal.Title.Value.ToString();
            }

            if (personal.PreferredName != null)
            {
                dto.PreferredName = personal.PreferredName;
            }

            if (personal.MiddleName != null)
            {
                dto.MiddleName = personal.MiddleName;
            }

            if (personal.LastName != null)
            {
                dto.LastName = personal.LastName;
            }

            if (personal.GenerationalSuffix != null)
            {
                dto.GenerationalSuffix = personal.GenerationalSuffix.Value.ToString();
            }

            if (personal.DateOfBirth != null)
            {
                dto.DateOfBirth = personal.DateOfBirth.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            if (personal.BiologicalSex != null)
            {
                dto.BiologicalSex = personal.BiologicalSex.Value.ToString();
            }

            dto.ProfessionalCredentials.AddRange(personal.ProfessionalCredentials);
            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="PersonalDTO"/> to <see cref="Personal"/>.
    /// </summary>
    ///
    /// <param name="personalDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(PersonalDTO personalDTO)
    {
        /// <summary>
        /// Converts the <see cref="PersonalDTO"/> to a <see cref="Personal"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="Personal"/> domain object.
        /// </returns>
        public Personal ToDomain()
        {
            return Personal.Create(
                personalDTO.FirstName,
                Enum.TryParse<NameTitle>(personalDTO.Title.ToNullIfEmpty(), out var title) ? title : null,
                personalDTO.PreferredName.ToNullIfEmpty(),
                personalDTO.MiddleName.ToNullIfEmpty(),
                personalDTO.LastName.ToNullIfEmpty(),
                Enum.TryParse<GenerationalSuffix>(personalDTO.GenerationalSuffix.ToNullIfEmpty(), out var suffix) ? suffix : null,
                personalDTO.ProfessionalCredentials,
                DateOnly.TryParse(personalDTO.DateOfBirth.ToNullIfEmpty(), out var dob) ? dob : null,
                Enum.TryParse<BiologicalSex>(personalDTO.BiologicalSex.ToNullIfEmpty(), out var sex) ? sex : null);
        }
    }
}

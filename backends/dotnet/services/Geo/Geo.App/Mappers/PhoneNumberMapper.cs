// -----------------------------------------------------------------------
// <copyright file="PhoneNumberMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;

/// <summary>
/// Mapper for converting between <see cref="PhoneNumber"/> and <see cref="PhoneNumberDTO"/>.
/// </summary>
public static class PhoneNumberMapper
{
    /// <summary>
    /// Maps <see cref="PhoneNumber"/> to <see cref="PhoneNumberDTO"/>.
    /// </summary>
    ///
    /// <param name="phoneNumber">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(PhoneNumber phoneNumber)
    {
        /// <summary>
        /// Converts the <see cref="PhoneNumber"/> domain object to a <see cref="PhoneNumberDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="PhoneNumberDTO"/> object.
        /// </returns>
        public PhoneNumberDTO ToDTO()
        {
            var dto = new PhoneNumberDTO
            {
                Value = phoneNumber.Value,
            };
            dto.Labels.AddRange(phoneNumber.Labels);
            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="PhoneNumberDTO"/> to <see cref="PhoneNumber"/>.
    /// </summary>
    ///
    /// <param name="phoneNumberDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(PhoneNumberDTO phoneNumberDTO)
    {
        /// <summary>
        /// Converts the <see cref="PhoneNumberDTO"/> to a <see cref="PhoneNumber"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="PhoneNumber"/> domain object.
        /// </returns>
        public PhoneNumber ToDomain()
        {
            return PhoneNumber.Create(phoneNumberDTO.Value, phoneNumberDTO.Labels);
        }
    }
}

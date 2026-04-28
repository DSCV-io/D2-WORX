// -----------------------------------------------------------------------
// <copyright file="EmailAddressMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;

/// <summary>
/// Mapper for converting between <see cref="EmailAddress"/> and <see cref="EmailAddressDTO"/>.
/// </summary>
public static class EmailAddressMapper
{
    /// <summary>
    /// Maps <see cref="EmailAddress"/> to <see cref="EmailAddressDTO"/>.
    /// </summary>
    ///
    /// <param name="emailAddress">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(EmailAddress emailAddress)
    {
        /// <summary>
        /// Converts the <see cref="EmailAddress"/> domain object to a <see cref="EmailAddressDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="EmailAddressDTO"/> object.
        /// </returns>
        public EmailAddressDTO ToDTO()
        {
            var dto = new EmailAddressDTO
            {
                Value = emailAddress.Value,
            };
            dto.Labels.AddRange(emailAddress.Labels);
            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="EmailAddressDTO"/> to <see cref="EmailAddress"/>.
    /// </summary>
    ///
    /// <param name="emailAddressDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(EmailAddressDTO emailAddressDTO)
    {
        /// <summary>
        /// Converts the <see cref="EmailAddressDTO"/> to a <see cref="EmailAddress"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="EmailAddress"/> domain object.
        /// </returns>
        public EmailAddress ToDomain()
        {
            return EmailAddress.Create(emailAddressDTO.Value, emailAddressDTO.Labels);
        }
    }
}

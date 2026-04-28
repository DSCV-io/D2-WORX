// -----------------------------------------------------------------------
// <copyright file="ContactMethodsMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using System.Collections.Immutable;
using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;

/// <summary>
/// Mapper for converting between <see cref="ContactMethods"/> and <see cref="ContactMethodsDTO"/>.
/// </summary>
public static class ContactMethodsMapper
{
    /// <summary>
    /// Maps <see cref="ContactMethods"/> to <see cref="ContactMethodsDTO"/>.
    /// </summary>
    ///
    /// <param name="contactMethods">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(ContactMethods contactMethods)
    {
        /// <summary>
        /// Converts the <see cref="ContactMethods"/> domain object to a <see cref="ContactMethodsDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="ContactMethodsDTO"/> object.
        /// </returns>
        public ContactMethodsDTO ToDTO()
        {
            var dto = new ContactMethodsDTO();
            dto.Emails.AddRange(contactMethods.Emails.Select(e => e.ToDTO()));
            dto.PhoneNumbers.AddRange(contactMethods.PhoneNumbers.Select(p => p.ToDTO()));
            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="ContactMethodsDTO"/> to <see cref="ContactMethods"/>.
    /// </summary>
    ///
    /// <param name="contactMethodsDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(ContactMethodsDTO contactMethodsDTO)
    {
        /// <summary>
        /// Converts the <see cref="ContactMethodsDTO"/> to a <see cref="ContactMethods"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="ContactMethods"/> domain object.
        /// </returns>
        public ContactMethods ToDomain()
        {
            return ContactMethods.Create(
                contactMethodsDTO.Emails.Select(e => e.ToDomain()).ToImmutableList(),
                contactMethodsDTO.PhoneNumbers.Select(p => p.ToDomain()).ToImmutableList());
        }
    }
}

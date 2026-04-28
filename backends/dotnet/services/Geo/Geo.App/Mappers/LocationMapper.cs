// -----------------------------------------------------------------------
// <copyright file="LocationMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using D2.Geo.Domain.Entities;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Mapper for converting between <see cref="Location"/> and <see cref="LocationDTO"/>.
/// </summary>
public static class LocationMapper
{
    /// <summary>
    /// Maps <see cref="Location"/> to <see cref="LocationDTO"/>.
    /// </summary>
    ///
    /// <param name="location">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(Location location)
    {
        /// <summary>
        /// Converts the <see cref="Location"/> domain object to a <see cref="LocationDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="LocationDTO"/> object.
        /// </returns>
        public LocationDTO ToDTO()
        {
            var dto = new LocationDTO
            {
                HashId = location.HashId,
                Coordinates = location.Coordinates?.ToDTO(),
                Address = location.Address?.ToDTO(),
            };

            // Optional fields — only set when non-null to avoid proto CheckNotNull.
            if (location.City != null)
            {
                dto.City = location.City;
            }

            if (location.PostalCode != null)
            {
                dto.PostalCode = location.PostalCode;
            }

            if (location.SubdivisionISO31662Code != null)
            {
                dto.SubdivisionIso31662Code = location.SubdivisionISO31662Code;
            }

            if (location.CountryISO31661Alpha2Code != null)
            {
                dto.CountryIso31661Alpha2Code = location.CountryISO31661Alpha2Code;
            }

            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="LocationDTO"/> to <see cref="Location"/>.
    /// </summary>
    ///
    /// <param name="locationDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(LocationDTO locationDTO)
    {
        /// <summary>
        /// Converts the <see cref="LocationDTO"/> to a <see cref="Location"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="Location"/> domain object.
        /// </returns>
        public Location ToDomain()
        {
            return Location.Create(
                locationDTO.Coordinates?.ToDomain(),
                locationDTO.Address?.ToDomain(),
                locationDTO.City.ToNullIfEmpty(),
                locationDTO.PostalCode.ToNullIfEmpty(),
                locationDTO.SubdivisionIso31662Code.ToNullIfEmpty(),
                locationDTO.CountryIso31661Alpha2Code.ToNullIfEmpty());
        }
    }
}

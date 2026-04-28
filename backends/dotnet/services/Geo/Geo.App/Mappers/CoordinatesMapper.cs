// -----------------------------------------------------------------------
// <copyright file="CoordinatesMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using D2.Geo.Domain.ValueObjects;
using D2.Services.Protos.Geo.V1;

/// <summary>
/// Mapper for converting between <see cref="Coordinates"/> and <see cref="CoordinatesDTO"/>.
/// </summary>
public static class CoordinatesMapper
{
    /// <summary>
    /// Maps <see cref="Coordinates"/> to <see cref="CoordinatesDTO"/>.
    /// </summary>
    ///
    /// <param name="coordinates">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(Coordinates coordinates)
    {
        /// <summary>
        /// Converts the <see cref="Coordinates"/> domain object to a <see cref="CoordinatesDTO"/>.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="CoordinatesDTO"/> object.
        /// </returns>
        public CoordinatesDTO ToDTO()
        {
            return new CoordinatesDTO
            {
                Latitude = coordinates.Latitude,
                Longitude = coordinates.Longitude,
            };
        }
    }

    /// <summary>
    /// Maps <see cref="CoordinatesDTO"/> to <see cref="Coordinates"/>.
    /// </summary>
    ///
    /// <param name="coordinatesDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(CoordinatesDTO coordinatesDTO)
    {
        /// <summary>
        /// Converts the <see cref="CoordinatesDTO"/> to a <see cref="Coordinates"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="Coordinates"/> domain object.
        /// </returns>
        public Coordinates ToDomain()
        {
            return Coordinates.Create(coordinatesDTO.Latitude, coordinatesDTO.Longitude);
        }
    }
}

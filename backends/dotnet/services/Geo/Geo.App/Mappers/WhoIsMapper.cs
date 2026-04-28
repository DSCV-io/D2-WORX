// -----------------------------------------------------------------------
// <copyright file="WhoIsMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Mappers;

using System.Globalization;
using D2.Geo.Domain.Entities;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Mapper for converting between <see cref="WhoIs"/> and <see cref="WhoIsDTO"/>.
/// </summary>
public static class WhoIsMapper
{
    /// <summary>
    /// Maps <see cref="WhoIs"/> to <see cref="WhoIsDTO"/>.
    /// </summary>
    ///
    /// <param name="whoIs">
    /// The domain object to be mapped to a DTO.
    /// </param>
    extension(WhoIs whoIs)
    {
        /// <summary>
        /// Converts the <see cref="WhoIs"/> domain object to a <see cref="WhoIsDTO"/>.
        /// </summary>
        ///
        /// <param name="location">
        /// Optional location entity to include in the DTO.
        /// </param>
        ///
        /// <returns>
        /// The mapped <see cref="WhoIsDTO"/> object.
        /// </returns>
        public WhoIsDTO ToDTO(Location? location = null)
        {
            var dto = new WhoIsDTO
            {
                HashId = whoIs.HashId,
                IpAddress = whoIs.IPAddress,
                Year = whoIs.Year,
                Month = whoIs.Month,
                Asn = whoIs.ASN ?? 0,
                IsAnonymous = whoIs.IsAnonymous ?? false,
                IsAnycast = whoIs.IsAnycast ?? false,
                IsHosting = whoIs.IsHosting ?? false,
                IsMobile = whoIs.IsMobile ?? false,
                IsSatellite = whoIs.IsSatellite ?? false,
                IsProxy = whoIs.IsProxy ?? false,
                IsRelay = whoIs.IsRelay ?? false,
                IsTor = whoIs.IsTor ?? false,
                IsVpn = whoIs.IsVPN ?? false,
                Location = location?.ToDTO(),
            };

            // Optional fields — only set when non-null to avoid proto CheckNotNull.
            if (whoIs.ASName != null)
            {
                dto.AsName = whoIs.ASName;
            }

            if (whoIs.ASDomain != null)
            {
                dto.AsDomain = whoIs.ASDomain;
            }

            if (whoIs.ASType != null)
            {
                dto.AsType = whoIs.ASType;
            }

            if (whoIs.CarrierName != null)
            {
                dto.CarrierName = whoIs.CarrierName;
            }

            if (whoIs.MCC != null)
            {
                dto.Mcc = whoIs.MCC;
            }

            if (whoIs.MNC != null)
            {
                dto.Mnc = whoIs.MNC;
            }

            if (whoIs.ASChanged != null)
            {
                dto.AsChanged = whoIs.ASChanged.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            if (whoIs.GeoChanged != null)
            {
                dto.GeoChanged = whoIs.GeoChanged.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            if (whoIs.PrivacyName != null)
            {
                dto.PrivacyName = whoIs.PrivacyName;
            }

            return dto;
        }
    }

    /// <summary>
    /// Maps <see cref="WhoIsDTO"/> to <see cref="WhoIs"/>.
    /// </summary>
    ///
    /// <param name="whoIsDTO">
    /// The DTO to be mapped to a domain object.
    /// </param>
    extension(WhoIsDTO whoIsDTO)
    {
        /// <summary>
        /// Converts the <see cref="WhoIsDTO"/> to a <see cref="WhoIs"/> domain object.
        /// </summary>
        ///
        /// <returns>
        /// The mapped <see cref="WhoIs"/> domain object.
        /// </returns>
        public WhoIs ToDomain()
        {
            return WhoIs.Create(
                whoIsDTO.IpAddress,
                whoIsDTO.Year,
                whoIsDTO.Month,
                whoIsDTO.Asn == 0 ? null : whoIsDTO.Asn,
                whoIsDTO.AsName.ToNullIfEmpty(),
                whoIsDTO.AsDomain.ToNullIfEmpty(),
                whoIsDTO.AsType.ToNullIfEmpty(),
                whoIsDTO.CarrierName.ToNullIfEmpty(),
                whoIsDTO.Mcc.ToNullIfEmpty(),
                whoIsDTO.Mnc.ToNullIfEmpty(),
                DateOnly.TryParse(whoIsDTO.AsChanged, out var asChanged) ? asChanged : null,
                DateOnly.TryParse(whoIsDTO.GeoChanged, out var geoChanged) ? geoChanged : null,
                whoIsDTO.IsAnonymous,
                whoIsDTO.IsAnycast,
                whoIsDTO.IsHosting,
                whoIsDTO.IsMobile,
                whoIsDTO.IsSatellite,
                whoIsDTO.IsProxy,
                whoIsDTO.IsRelay,
                whoIsDTO.IsTor,
                whoIsDTO.IsVpn,
                whoIsDTO.PrivacyName.ToNullIfEmpty(),
                whoIsDTO.Location?.HashId.ToNullIfEmpty());
        }
    }
}

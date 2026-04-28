// -----------------------------------------------------------------------
// <copyright file="IRead.GetReferenceData.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.Repository.Handlers.R;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IRead
{
    /// <summary>
    /// Handler for getting geographic reference data.
    /// </summary>
    public interface IGetReferenceDataHandler
        : IHandler<GetReferenceDataInput, GetReferenceDataOutput>;

    /// <summary>
    /// Input for getting geographic reference data.
    /// </summary>
    public record GetReferenceDataInput;

    /// <summary>
    /// Output for getting geographic reference data.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data retrieved.
    /// </param>
    public record GetReferenceDataOutput(GeoRefData Data);
}

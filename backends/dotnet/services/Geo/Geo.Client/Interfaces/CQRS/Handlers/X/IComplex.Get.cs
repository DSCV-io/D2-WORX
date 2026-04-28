// -----------------------------------------------------------------------
// <copyright file="IComplex.Get.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.Interfaces.CQRS.Handlers.X;

using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;

public partial interface IComplex
{
    /// <summary>
    /// Handler for getting geographic reference data.
    /// </summary>
    public interface IGetHandler : IHandler<GetInput, GetOutput>;

    /// <summary>
    /// Input for getting geographic reference data.
    /// </summary>
    public record GetInput;

    /// <summary>
    /// Output for getting geographic reference data.
    /// </summary>
    ///
    /// <param name="Data">
    /// The geographic reference data retrieved.
    /// </param>
    public record GetOutput(GeoRefData Data);
}

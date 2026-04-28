// -----------------------------------------------------------------------
// <copyright file="ResultExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Gateways.REST;

using D2.Services.Protos.Common.V1;
using D2.Shared.Result.Extensions;
using D2.Shared.Utilities.Serialization;

/// <summary>
/// Extension methods for converting D2ResultProto to HTTP results.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a <see cref="D2ResultProto"/> instance to an HTTP result.
    /// </summary>
    ///
    /// <param name="proto">
    /// The <see cref="D2ResultProto"/> instance to convert.
    /// </param>
    extension(D2ResultProto proto)
    {
        /// <summary>
        /// Converts the D2ResultProto instance to an HTTP result.
        /// </summary>
        ///
        /// <param name="data">
        /// The optional data payload to include in the HTTP result.
        /// </param>
        ///
        /// <typeparam name="TData">
        /// The type of the data payload.
        /// </typeparam>
        ///
        /// <returns>
        /// An <see cref="IResult"/> representing the HTTP result.
        /// </returns>
        public IResult ToHttpResult<TData>(TData? data)
        {
            var result = proto.ToD2Result(data);
            return Results.Json(
                data: result,
                options: SerializerOptions.SR_IgnoreCycles,
                statusCode: (int)result.StatusCode);
        }
    }
}

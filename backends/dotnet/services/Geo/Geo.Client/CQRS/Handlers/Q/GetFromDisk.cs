// -----------------------------------------------------------------------
// <copyright file="GetFromDisk.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.Q;

using System.Net;
using D2.Services.Protos.Geo.V1;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// ReSharper disable AccessToStaticMemberViaDerivedType
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.IGetFromDiskHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.GetFromDiskInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.Q.IQueries.GetFromDiskOutput;

/// <summary>
/// Handler for getting georeference data from disk.
/// </summary>
public partial class GetFromDisk : BaseHandler<GetFromDisk, I, O>, H
{
    private readonly string r_filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetFromDisk"/> class.
    /// </summary>
    ///
    /// <param name="config">
    /// The configuration instance.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public GetFromDisk(
        IConfiguration config,
        IHandlerContext context)
        : base(context)
    {
        var dataDir = config[Constants.LOCAL_FILES_PATH_CONFIG_KEY] ?? "./data";
        Directory.CreateDirectory(dataDir);
        r_filePath = Path.Combine(dataDir, Constants.GEO_REF_DATA_FILE_NAME);
    }

    /// <inheritdoc />
    protected override HandlerOptions DefaultOptions => new(LogInput: false, LogOutput: false);

    /// <summary>
    /// Executes the handler to get georeference data from disk.
    /// </summary>
    ///
    /// <param name="input">
    /// The input parameters for the handler.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// The result of the get operation, containing the georeference data if found.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(r_filePath))
            {
                return D2Result<O?>.NotFound();
            }

            var bytes = await File.ReadAllBytesAsync(r_filePath, ct);
            var data = GeoRefData.Parser.ParseFrom(bytes);

            return D2Result<O?>.Ok(new O(data));
        }
        catch (Google.Protobuf.InvalidProtocolBufferException ex)
        {
            LogCorruptedDataOnDisk(Context.Logger, ex, TraceId);

            return D2Result<O?>.Fail(
                [TK.Geo.Errors.CORRUPTED_DATA_ON_DISK],
                HttpStatusCode.InternalServerError,
                errorCode: ErrorCodes.COULD_NOT_BE_DESERIALIZED);
        }
        catch (IOException ex)
        {
            LogDiskReadFailed(Context.Logger, ex, TraceId);

            return D2Result<O?>.Fail(
                [TK.Geo.Errors.DISK_READ_FAILED],
                HttpStatusCode.InternalServerError);
        }

        // Let the base handler catch any other exceptions.
    }

    /// <summary>
    /// Logs an error when georeference data on disk is corrupted and cannot be parsed.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to parse georeference data from disk. TraceId: {TraceId}")]
    private static partial void LogCorruptedDataOnDisk(ILogger logger, Exception ex, string? traceId);

    /// <summary>
    /// Logs an error when an IOException occurs while reading georeference data from disk.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "IOException occurred while reading georeference data from disk. TraceId: {TraceId}")]
    private static partial void LogDiskReadFailed(ILogger logger, Exception ex, string? traceId);
}

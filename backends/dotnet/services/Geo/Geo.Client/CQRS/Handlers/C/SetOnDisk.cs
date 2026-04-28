// -----------------------------------------------------------------------
// <copyright file="SetOnDisk.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Client.CQRS.Handlers.C;

using System.Net;
using D2.Shared.Handler;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Constants;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using H = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.ISetOnDiskHandler;
using I = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetOnDiskInput;
using O = D2.Geo.Client.Interfaces.CQRS.Handlers.C.ICommands.SetOnDiskOutput;

/// <summary>
/// Handler for setting georeference data on disk.
/// </summary>
public partial class SetOnDisk : BaseHandler<SetOnDisk, I, O>, H
{
    private readonly string r_filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetOnDisk"/> class.
    /// </summary>
    ///
    /// <param name="config">
    /// The configuration.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public SetOnDisk(
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
    /// Executes the handler to set georeference data on disk.
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
    /// The result of the set operation.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = input.Data.ToByteArray();
            await File.WriteAllBytesAsync(r_filePath, bytes, ct);

            return D2Result<O?>.Ok(new O());
        }
        catch (IOException ex)
        {
            LogDiskWriteFailed(Context.Logger, ex, TraceId);

            return D2Result<O?>.Fail(
                [TK.Geo.Errors.DISK_WRITE_FAILED],
                HttpStatusCode.InternalServerError);
        }

        // Let the base handler catch any other exceptions.
    }

    /// <summary>
    /// Logs an error when an IOException occurs while writing georeference data to disk.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "IOException occurred while writing georeference data to disk. TraceId: {TraceId}")]
    private static partial void LogDiskWriteFailed(ILogger logger, Exception ex, string? traceId);
}

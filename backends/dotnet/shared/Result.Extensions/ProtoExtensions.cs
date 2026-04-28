// -----------------------------------------------------------------------
// <copyright file="ProtoExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result.Extensions;

using System.Net;
using D2.Services.Protos.Common.V1;
using D2.Shared.Utilities.Extensions;
using Grpc.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for converting D2Result to its protobuf representation.
/// </summary>
public static partial class ProtoExtensions
{
    /// <summary>
    /// Converts a <see cref="D2Result"/> instance to its protobuf representation
    /// </summary>
    ///
    /// <param name="result">
    /// The <see cref="D2Result"/> instance to convert.
    /// </param>
    extension(D2Result result)
    {
        /// <summary>
        /// Converts the D2Result instance to its protobuf representation.
        /// </summary>
        ///
        /// <returns>
        /// A <see cref="D2ResultProto"/> representing the D2Result.
        /// </returns>
        public D2ResultProto ToProto()
        {
            // Map basic properties.
            var proto = new D2ResultProto
            {
                Success = result.Success,
                StatusCode = (int)result.StatusCode,
            };

            if (result.ErrorCode != null)
            {
                proto.ErrorCode = result.ErrorCode;
            }

            if (result.TraceId != null)
            {
                proto.TraceId = result.TraceId;
            }

            // Map messages.
            proto.Messages.AddRange(result.Messages);

            // Map input errors.
            foreach (var inputError in result.InputErrors)
            {
                if (inputError.Count is 0)
                {
                    continue;
                }

                var error = new InputErrorProto { Field = inputError[0] };
                error.Errors.AddRange(inputError.Skip(1));
                proto.InputErrors.Add(error);
            }

            // Return the populated proto object.
            return proto;
        }
    }

    /// <summary>
    /// Converts a <see cref="D2ResultProto"/> instance to its corresponding
    /// <see cref="D2Result{TData}"/> representation.
    /// </summary>
    ///
    /// <param name="proto">
    /// The <see cref="D2ResultProto"/> instance to convert.
    /// </param>
    extension(D2ResultProto proto)
    {
        /// <summary>
        /// Converts the D2ResultProto instance to its corresponding D2Result{TData} representation.
        /// </summary>
        ///
        /// <param name="data">
        /// The data of type TData to include in the D2Result. Optional.
        /// </param>
        /// <typeparam name="TData">
        /// The type of the data to be included in the D2Result.
        /// </typeparam>
        ///
        /// <returns>
        /// A <see cref="D2Result{TData}"/> representing the result.
        /// </returns>
        public D2Result<TData> ToD2Result<TData>(TData? data = default)
        {
            var result = new D2Result<TData>(
                success: proto.Success,
                data: data,
                messages: proto.Messages.ToList(),
                inputErrors: proto.InputErrors
                    .Select(ie => new List<string> { ie.Field }.Concat(ie.Errors).ToList())
                    .ToList(),
                statusCode: (HttpStatusCode)proto.StatusCode,
                errorCode: proto.ErrorCode.Falsey() ? null : proto.ErrorCode,
                traceId: proto.TraceId.Falsey() ? null : proto.TraceId);

            return result;
        }
    }

    /// <summary>
    /// Handles an asynchronous gRPC unary call, converting the response to a D2Result{TData}.
    /// </summary>
    ///
    /// <param name="call">
    /// The asynchronous gRPC unary call to handle.
    /// </param>
    ///
    /// <typeparam name="TProto">
    /// The type of the protobuf response from the gRPC call.
    /// </typeparam>
    extension<TProto>(AsyncUnaryCall<TProto> call)
    {

        /// <summary>
        /// Handles the gRPC call, converting the response to a D2Result{TData}.
        /// </summary>
        ///
        /// <param name="resultSelector">
        /// A function to select the D2ResultProto from the gRPC response.
        /// </param>
        /// <param name="dataSelector">
        /// A function to select the data of type TData from the gRPC response.
        /// </param>
        /// <param name="logger">
        /// An optional logger for logging errors.
        /// </param>
        /// <param name="traceId">
        /// An optional trace identifier for logging and diagnostics.
        /// </param>
        ///
        /// <typeparam name="TData">
        /// The type of the data to be included in the D2Result.
        /// </typeparam>
        ///
        /// <returns>
        /// A <see cref="D2Result{TData}"/> representing the result of the gRPC call.
        /// </returns>
        public async Task<D2Result<TData>> HandleAsync<TData>(
            Func<TProto, D2ResultProto> resultSelector,
            Func<TProto, TData?> dataSelector,
            ILogger? logger = null,
            string? traceId = null)
        {
            try
            {
                var response = await call;
                var proto = resultSelector(response);
                var data = dataSelector(response);
                return proto.ToD2Result(data);
            }
            catch (RpcException ex)
            {
                if (logger is not null)
                {
                    LogGrpcTransportFailure(logger, ex, ex.StatusCode, traceId);
                }

                return D2Result<TData>.ServiceUnavailable(traceId: traceId);
            }
            catch (Exception ex)
            {
                if (logger is not null)
                {
                    LogGrpcUnexpectedError(logger, ex, traceId);
                }

                return D2Result<TData>.UnhandledException(traceId: traceId);
            }
        }
    }

    /// <summary>
    /// Logs a gRPC transport failure.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "gRPC transport failure. StatusCode: {StatusCode}, TraceId: {TraceId}")]
    private static partial void LogGrpcTransportFailure(ILogger logger, Exception ex, StatusCode statusCode, string? traceId);

    /// <summary>
    /// Logs an unexpected error during a gRPC call.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Unexpected error during gRPC call. TraceId: {TraceId}")]
    private static partial void LogGrpcUnexpectedError(ILogger logger, Exception ex, string? traceId);
}

// -----------------------------------------------------------------------
// <copyright file="GrpcResultCodecFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Result.Grpc;
using DcsvIo.D2.Utilities.Extensions;
using Google.Protobuf;
using Xunit;

/// <summary>
/// Emits the binary cross-runtime parity fixture for the gRPC result codec
/// (.NET → node direction). For each representative <see cref="D2Result"/>
/// shape, calls <see cref="ProtoExtensions.ToProto"/> to obtain the
/// <c>D2ResultProto</c> wire envelope, serializes it to binary bytes, and
/// captures the original result's fields as the <c>expected</c> block. The
/// TS-side parity test reads
/// <c>fixtures/grpc-result-codec/cases.json</c>, decodes each
/// <c>protoBase64</c> entry via <c>D2ResultProto.decode</c>, runs it through
/// <c>d2ResultFromProto</c>, and asserts field-by-field equality against
/// <c>expected</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Direction</strong>: .NET → node is the primary path (node is the
/// gRPC consumer; a node BFF calling a .NET service). Binary bytes are
/// deterministic — a given <see cref="D2Result"/> shape always produces the
/// same proto binary — so the fixture is safe to commit.
/// </para>
/// <para>
/// <strong>Shapes covered</strong>: <c>Ok</c>, <c>Ok-with-trace-id</c>,
/// <c>NotFound</c>, <c>Conflict</c>, <c>ValidationFailed</c> (multi-field
/// with params), <c>Unauthorized</c>, <c>ServiceUnavailable</c>,
/// <c>SomeFound</c>, <c>TooManyRequests</c>, <c>PayloadTooLarge</c>,
/// <c>UnhandledException</c>, <c>Canceled</c>, and a raw <c>Fail</c> with
/// a non-catalog error code (category absent). Together these exercise every
/// <see cref="ErrorCategory"/> value and the category-absent path.
/// </para>
/// </remarks>
public sealed class GrpcResultCodecFixtureEmitter
{
    private const string _CATALOG = "grpc-result-codec";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Cases()
    {
        var cases = new List<object?>
        {
            // ── Success ──────────────────────────────────────────────────
            BuildCase(
                "ok",
                D2Result.Ok()),

            BuildCase(
                "ok-with-trace-id",
                D2Result.Ok(traceId: "aabbccddeeff00112233445566778899")),

            // ── Semantic failures — one per ErrorCategory ─────────────────

            // not_found
            BuildCase(
                "not-found",
                D2Result.NotFound(
                    messages: [new TKMessage("common_errors_NOT_FOUND")])),

            // conflict
            BuildCase(
                "conflict",
                D2Result.Conflict(
                    messages: [new TKMessage("common_errors_CONFLICT")])),

            // validation_failure — multi-field, multi-message, with params
            BuildCase(
                "validation-failed",
                D2Result.ValidationFailed(
                    inputErrors:
                    [
                        new InputError(
                            "email",
                            [new TKMessage("common_validation_EMAIL_INVALID")]),
                        new InputError(
                            "phone",
                            [
                                new TKMessage(
                                    "common_validation_FORMAT_INVALID",
                                    new Dictionary<string, string>(StringComparer.Ordinal)
                                    {
                                        ["field"] = "phone",
                                        ["rule"] = "e164",
                                    }),
                                new TKMessage(
                                    "common_validation_TOO_LONG",
                                    new Dictionary<string, string>(StringComparer.Ordinal)
                                    {
                                        ["max"] = "15",
                                    }),
                            ]),
                    ])),

            // policy_denied
            BuildCase(
                "unauthorized",
                D2Result.Unauthorized(
                    messages: [new TKMessage("common_errors_UNAUTHORIZED")])),

            // infrastructure_unavailable
            BuildCase(
                "service-unavailable",
                D2Result.ServiceUnavailable(
                    messages: [new TKMessage("common_errors_SERVICE_UNAVAILABLE")])),

            // partial_success
            BuildCase(
                "some-found",
                D2Result.SomeFound()),

            // rate_limited
            BuildCase(
                "too-many-requests",
                D2Result.TooManyRequests(
                    messages: [new TKMessage("common_errors_TOO_MANY_REQUESTS")])),

            // payload_too_large
            BuildCase(
                "payload-too-large",
                D2Result.PayloadTooLarge(
                    messages: [new TKMessage("common_errors_PAYLOAD_TOO_LARGE")])),

            // internal_error
            BuildCase(
                "unhandled-exception",
                D2Result.UnhandledException(
                    messages: [new TKMessage("common_errors_UNKNOWN")])),

            // validation_failure (Canceled — non-obvious category per spec)
            BuildCase(
                "canceled",
                D2Result.Canceled(
                    messages: [new TKMessage("common_errors_CANCELED")])),

            // no-catalog code — raw Fail with category absent
            BuildCase(
                "raw-fail-no-category",
                D2Result.Fail(
                    messages: [new TKMessage("common_errors_UNKNOWN")],
                    statusCode: HttpStatusCode.UnprocessableContent,
                    errorCode: "CUSTOM_NON_CATALOG_CODE")),
        };

        FixturePathHelpers.WriteFixture(_CATALOG, "cases", cases);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static SortedDictionary<string, object?> BuildCase(
        string name,
        D2Result result)
    {
        var proto = result.ToProto();
        var bytes = proto.ToByteArray();
        var protoBase64 = Convert.ToBase64String(bytes);

        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["protoBase64"] = protoBase64,
            ["expected"] = BuildExpected(result),
        };
    }

    private static SortedDictionary<string, object?> BuildExpected(D2Result result)
    {
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["success"] = result.Success,
            ["statusCode"] = (int)result.StatusCode,
            ["errorCode"] = result.ErrorCode.Falsey() ? null : result.ErrorCode,
            ["category"] = result.Category.HasValue ? result.Category.Value.ToWire() : null,
            ["traceId"] = result.TraceId.Falsey() ? null : result.TraceId,
            ["messages"] = result.Messages.Select(m => (object?)BuildMessage(m)).ToList(),
            ["inputErrors"] = result.InputErrors.Select(ie => (object?)BuildInputError(ie)).ToList(),
        };
    }

    private static SortedDictionary<string, object?> BuildMessage(TKMessage msg)
    {
        var d = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = msg.Key,
        };

        if (msg.Parameters is not null && msg.Parameters.Count > 0)
        {
            var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in msg.Parameters)
                sorted[kv.Key] = kv.Value;
            d["params"] = sorted;
        }

        return d;
    }

    private static SortedDictionary<string, object?> BuildInputError(InputError ie)
    {
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["field"] = ie.Field,
            ["errors"] = ie.Errors.Select(e => (object?)BuildMessage(e)).ToList(),
        };
    }
}

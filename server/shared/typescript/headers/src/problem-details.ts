// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";
import { HttpStatusCode } from "@d2/result";

/**
 * Type-URI prefix for D2 ProblemDetails. Mirrors the .NET
 * `D2ProblemDetailsExtensions` PROBLEM_TYPE_URI_PREFIX value used in
 * `D2.Shared.Auth.Http.ProblemDetails`. Cross-language wire MUST
 * stay in sync — change here, change in the .NET extensions class.
 */
export const PROBLEM_TYPE_URI_PREFIX = "https://problems.d2-worx.com/";

/**
 * Extension keys on the ProblemDetails JSON body. These keys are emitted
 * by the .NET `D2.Shared.Auth.Http.ProblemDetails.D2ProblemDetailsExtensions`
 * class on the Edge side; the BFF mirrors them on the rejection envelopes
 * it returns to the browser. Cross-language wire MUST stay in sync.
 */
export const ProblemDetailsExtensionKeys = {
  /** Machine-readable error code (mirrors AuthErrorCodes / ErrorCodes values). */
  ERROR_CODE: "d2_error_code",
  /** Localized + parameterized message bundle for client-side rendering. */
  MESSAGES: "d2_messages",
  /** W3C trace id for diagnostic correlation. */
  TRACE_ID: "traceId",
} as const;

/**
 * RFC 7807 ProblemDetails body shape as emitted by the BFF on guard
 * rejections. Fields with `unknown` value type carry the raw extension
 * payload — extension keys are spec'd in `ProblemDetailsExtensionKeys`.
 */
export interface ProblemDetailsBody {
  readonly type: string;
  readonly title: string;
  readonly status: number;
  readonly detail?: string;
  readonly instance: string;
  readonly [key: string]: unknown;
}

/**
 * Options for `toProblemDetails`.
 */
export interface ProblemDetailsOptions {
  /**
   * Request URL path that originated the failure — RFC 7807 §3.1 instance.
   */
  readonly instance: string;
  /**
   * Optional override for the ProblemDetails `title` field. Defaults to a
   * generic per-status string.
   */
  readonly title?: string;
  /** Optional override for the ProblemDetails `detail` field. */
  readonly detail?: string;
}

/**
 * Build an RFC 7807 ProblemDetails body from a `D2Result` failure. Mirrors
 * the .NET `D2.Shared.Auth.Http.ProblemDetails.D2ProblemDetailsExtensions`
 * shape. Extension keys (`d2_error_code`, `d2_messages`, `traceId`) match
 * the .NET emitter byte-for-byte so the browser can rely on the same
 * contract regardless of which side rejected the request.
 */
export function toProblemDetails(
  failure: D2Result<unknown>,
  opts: ProblemDetailsOptions,
): ProblemDetailsBody {
  const status = failure.statusCode;
  const errorCode = failure.errorCode ?? "UNKNOWN";
  const typeUri = `${PROBLEM_TYPE_URI_PREFIX}${_kebabize(errorCode)}`;
  const body: Record<string, unknown> = {
    type: typeUri,
    title: opts.title ?? _defaultTitleForStatus(status),
    status,
    instance: opts.instance,
    [ProblemDetailsExtensionKeys.ERROR_CODE]: errorCode,
  };
  if (opts.detail !== undefined) body["detail"] = opts.detail;
  if (failure.messages.length > 0) {
    body[ProblemDetailsExtensionKeys.MESSAGES] = failure.messages;
  }
  if (failure.traceId !== undefined) {
    body[ProblemDetailsExtensionKeys.TRACE_ID] = failure.traceId;
  }
  return body as ProblemDetailsBody;
}

function _kebabize(errorCode: string): string {
  return errorCode.toLowerCase().replace(/_/g, "-");
}

function _defaultTitleForStatus(status: number): string {
  switch (status) {
    case HttpStatusCode.BadRequest:
      return "Bad Request";
    case HttpStatusCode.Unauthorized:
      return "Unauthorized";
    case HttpStatusCode.Forbidden:
      return "Forbidden";
    case HttpStatusCode.NotFound:
      return "Not Found";
    case HttpStatusCode.Conflict:
      return "Conflict";
    case HttpStatusCode.RequestEntityTooLarge:
      return "Payload Too Large";
    case HttpStatusCode.TooManyRequests:
      return "Too Many Requests";
    case HttpStatusCode.InternalServerError:
      return "Internal Server Error";
    case HttpStatusCode.ServiceUnavailable:
      return "Service Unavailable";
    default:
      return "Error";
  }
}

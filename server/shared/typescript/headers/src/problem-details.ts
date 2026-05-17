// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { D2Result } from "@d2/result";

import {
  defaultTitleForStatus,
  PROBLEM_TYPE_URI_PREFIX,
  ProblemDetailsExtensionKeys,
} from "./problem-details.g.js";

/**
 * RFC 7807 ProblemDetails body shape as emitted by the BFF on guard
 * rejections. Hand-written — RFC 7807 is an external standard, not a
 * D²-defined catalog. Extension key NAMES are spec-driven (see
 * `./problem-details.g.ts` for the codegen-emitted
 * `ProblemDetailsExtensionKeys`).
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
   * generic per-status string sourced from the spec-driven
   * `defaultTitleForStatus`.
   */
  readonly title?: string;
  /** Optional override for the ProblemDetails `detail` field. */
  readonly detail?: string;
}

/**
 * Build an RFC 7807 ProblemDetails body from a `D2Result` failure. The
 * wire-format catalog (`PROBLEM_TYPE_URI_PREFIX`,
 * `ProblemDetailsExtensionKeys`, `defaultTitleForStatus`) is codegen-emitted
 * from `contracts/problem-details/problem-details.spec.json` — the SAME
 * spec drives the .NET-side `D2.Shared.Auth.Http.ProblemDetails.D2ProblemDetailsExtensions`
 * partial class, so cross-language drift is structurally impossible.
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
    title: opts.title ?? defaultTitleForStatus(status),
    status,
    instance: opts.instance,
    [ProblemDetailsExtensionKeys.ERROR_CODE]: errorCode,
  };
  if (opts.detail !== undefined) body["detail"] = opts.detail;
  if (failure.messages.length > 0) {
    body[ProblemDetailsExtensionKeys.MESSAGES] = failure.messages;
  }
  if (failure.inputErrors.length > 0) {
    body[ProblemDetailsExtensionKeys.INPUT_ERRORS] = failure.inputErrors;
  }
  if (failure.traceId !== undefined) {
    body[ProblemDetailsExtensionKeys.TRACE_ID] = failure.traceId;
  }
  return body as ProblemDetailsBody;
}

function _kebabize(errorCode: string): string {
  return errorCode.toLowerCase().replace(/_/g, "-");
}

// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type DlqFailureCause,
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "@d2/messaging-abstractions";
import { sanitizedErrorRender } from "@d2/logging";
import type { D2Result } from "@d2/result";

const _DETAIL_MAX_CHARS = 256;

/**
 * Fallback `errorCode` for a handler result failure that carries no error code.
 * Wire-mirrors the .NET sibling's `result.ErrorCode ?? "UNKNOWN"` sentinel
 * (`DlqFailureHeaderBuilder.cs:76`) so both runtimes emit an identical DLQ
 * header value. Kept as a local named constant on this side (the file's
 * `_DETAIL_MAX_CHARS` pattern); promoting BOTH runtimes to a single
 * spec-emitted constant is a tracked follow-up (touches out-of-diff .NET).
 */
const _UNKNOWN_ERROR_CODE = "UNKNOWN";

/** Optional correlation / provenance fields shared by every builder. */
export interface DlqFailureContext {
  readonly attemptCount?: number;
  readonly traceId?: string;
  readonly nackedBy?: string;
}

/**
 * Builds the `x-d2-failure-reason` header value (JSON-encoded
 * `DlqFailureMetadata`) attached when the consumer republishes a message to its
 * DLX. Byte-compatible with the .NET `DlqFailureHeaderBuilder`: field names come
 * from the spec-emitted `DlqFailureMetadataFields`, causes from
 * `DlqFailureCauses`, and null optionals are OMITTED (matching the .NET
 * `WhenWritingNull` policy). PII discipline: `detail` is NEVER built from an
 * error's message (handler code can interpolate user input) — for a result
 * failure it joins the result's translation-token keys; for every other cause
 * it stays absent.
 */
export const DlqFailureHeaderBuilder = {
  /** Header for a handler exception. `errorCode` = the error's type name. */
  fromException(error: unknown, ctx: DlqFailureContext = {}): string {
    return encode({
      cause: DlqFailureCauses.HANDLER_EXCEPTION,
      errorCode: sanitizedErrorRender(error).name,
      detail: undefined,
      ctx,
    });
  },

  /** Header for a handler result failure. `detail` = joined message keys. */
  fromResult(result: D2Result, ctx: DlqFailureContext = {}): string {
    const detail = truncate(result.messages.map((m) => m.key).join("; "));
    return encode({
      cause: DlqFailureCauses.HANDLER_RESULT_FAILURE,
      errorCode: result.errorCode ?? _UNKNOWN_ERROR_CODE,
      detail,
      ctx,
    });
  },

  /** Header for a retries-exhausted DLQ route (handler never invoked). */
  fromRetriesExhausted(
    attemptCount: number,
    ctx: DlqFailureContext = {},
  ): string {
    return encode({
      cause: DlqFailureCauses.RETRIES_EXHAUSTED,
      errorCode: DlqFailureCauses.RETRIES_EXHAUSTED,
      detail: undefined,
      ctx: { ...ctx, attemptCount },
    });
  },

  /** Header for a body decrypt / deserialize boundary failure. */
  fromBoundary(
    cause: DlqFailureCause,
    error: unknown,
    ctx: DlqFailureContext = {},
  ): string {
    return encode({
      cause,
      errorCode: sanitizedErrorRender(error).name,
      detail: undefined,
      ctx,
    });
  },
} as const;

interface EncodeInput {
  readonly cause: string;
  readonly errorCode: string;
  readonly detail: string | undefined;
  readonly ctx: DlqFailureContext;
}

function encode(input: EncodeInput): string {
  // Insertion order mirrors the .NET DlqFailureMetadata record; undefined
  // optionals are omitted by JSON.stringify (matching WhenWritingNull).
  const meta: Record<string, unknown> = {
    [DlqFailureMetadataFields.CAUSE]: input.cause,
    [DlqFailureMetadataFields.ERROR_CODE]: input.errorCode,
    [DlqFailureMetadataFields.DETAIL]: input.detail,
    [DlqFailureMetadataFields.ATTEMPT_COUNT]: input.ctx.attemptCount ?? 0,
    [DlqFailureMetadataFields.TRACE_ID]: input.ctx.traceId,
    [DlqFailureMetadataFields.NACKED_BY]: input.ctx.nackedBy,
  };
  return JSON.stringify(meta);
}

function truncate(input: string): string | undefined {
  if (input.length === 0) return undefined;

  return input.length <= _DETAIL_MAX_CHARS
    ? input
    : input.slice(0, _DETAIL_MAX_CHARS);
}

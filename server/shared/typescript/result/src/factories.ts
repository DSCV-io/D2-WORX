// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { D2Result } from "./d2-result.js";
import { ErrorCodes } from "./error-codes.g.js";
import { HttpStatusCode } from "./http-status-codes.js";
import type { InputError } from "./input-error.js";
import { tk, type TKMessage } from "./tk-message.js";

/**
 * Default TK keys — mirror `TK.Common.Errors.*` from the .NET TK SrcGen.
 * On the TS side the canonical TK source-of-truth is Paraglide; these
 * literal-keyed defaults exist so server-emitted defaults stay
 * cross-language-aligned even when no caller-provided messages are passed.
 */
const DEFAULTS = {
  NOT_FOUND: tk("TK.Common.Errors.NOT_FOUND"),
  FORBIDDEN: tk("TK.Common.Errors.FORBIDDEN"),
  UNAUTHORIZED: tk("TK.Common.Errors.UNAUTHORIZED"),
  VALIDATION_FAILED: tk("TK.Common.Errors.VALIDATION_FAILED"),
  CONFLICT: tk("TK.Common.Errors.CONFLICT"),
  SERVICE_UNAVAILABLE: tk("TK.Common.Errors.SERVICE_UNAVAILABLE"),
  UNKNOWN: tk("TK.Common.Errors.UNKNOWN"),
  PAYLOAD_TOO_LARGE: tk("TK.Common.Errors.PAYLOAD_TOO_LARGE"),
  CANCELED: tk("TK.Common.Errors.CANCELED"),
  SOME_FOUND: tk("TK.Common.Errors.SOME_FOUND"),
  TOO_MANY_REQUESTS: tk("TK.Common.Errors.TOO_MANY_REQUESTS"),
} as const;

interface BasicOpts {
  messages?: readonly TKMessage[];
  traceId?: string;
}

interface CodedOpts extends BasicOpts {
  errorCode?: string;
}

interface ValidationFailedOpts extends CodedOpts {
  inputErrors?: readonly InputError[];
}

interface FailOpts extends ValidationFailedOpts {
  statusCode?: HttpStatusCode;
}

/** Create a successful result carrying an optional typed payload. */
export function ok<T = void>(data?: T, traceId?: string): D2Result<T> {
  return new D2Result<T>({
    success: true,
    data,
    traceId,
  });
}

/** HTTP 201. Use for new-resource creation. */
export function created(opts: BasicOpts = {}): D2Result<void> {
  return new D2Result<void>({
    success: true,
    messages: opts.messages,
    statusCode: HttpStatusCode.Created,
    traceId: opts.traceId,
  });
}

/**
 * Raw fail. Use only when no semantic factory matches. Defaults to HTTP 400.
 */
export function fail<T = void>(opts: FailOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages,
    inputErrors: opts.inputErrors,
    statusCode: opts.statusCode,
    errorCode: opts.errorCode,
    traceId: opts.traceId,
  });
}

/** HTTP 404 / `NOT_FOUND`. */
export function notFound<T = void>(opts: BasicOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.NOT_FOUND],
    statusCode: HttpStatusCode.NotFound,
    errorCode: ErrorCodes.NOT_FOUND,
    traceId: opts.traceId,
  });
}

/** HTTP 401 / `UNAUTHORIZED` (override-able). */
export function unauthorized<T = void>(opts: CodedOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.UNAUTHORIZED],
    statusCode: HttpStatusCode.Unauthorized,
    errorCode: opts.errorCode ?? ErrorCodes.UNAUTHORIZED,
    traceId: opts.traceId,
  });
}

/** HTTP 403 / `FORBIDDEN` (override-able). */
export function forbidden<T = void>(opts: CodedOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.FORBIDDEN],
    statusCode: HttpStatusCode.Forbidden,
    errorCode: opts.errorCode ?? ErrorCodes.FORBIDDEN,
    traceId: opts.traceId,
  });
}

/** HTTP 400 / `VALIDATION_FAILED` (override-able) plus per-field errors. */
export function validationFailed<T = void>(
  opts: ValidationFailedOpts = {},
): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.VALIDATION_FAILED],
    inputErrors: opts.inputErrors,
    statusCode: HttpStatusCode.BadRequest,
    errorCode: opts.errorCode ?? ErrorCodes.VALIDATION_FAILED,
    traceId: opts.traceId,
  });
}

/** HTTP 409 / `CONFLICT`. */
export function conflict<T = void>(opts: BasicOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.CONFLICT],
    statusCode: HttpStatusCode.Conflict,
    errorCode: ErrorCodes.CONFLICT,
    traceId: opts.traceId,
  });
}

/** HTTP 503 / `SERVICE_UNAVAILABLE` (override-able). */
export function serviceUnavailable<T = void>(
  opts: CodedOpts = {},
): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.SERVICE_UNAVAILABLE],
    statusCode: HttpStatusCode.ServiceUnavailable,
    errorCode: opts.errorCode ?? ErrorCodes.SERVICE_UNAVAILABLE,
    traceId: opts.traceId,
  });
}

/** HTTP 500 / `UNHANDLED_EXCEPTION`. */
export function unhandledException<T = void>(
  opts: BasicOpts = {},
): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.UNKNOWN],
    statusCode: HttpStatusCode.InternalServerError,
    errorCode: ErrorCodes.UNHANDLED_EXCEPTION,
    traceId: opts.traceId,
  });
}

/** HTTP 413 / `PAYLOAD_TOO_LARGE`. */
export function payloadTooLarge<T = void>(opts: BasicOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.PAYLOAD_TOO_LARGE],
    statusCode: HttpStatusCode.RequestEntityTooLarge,
    errorCode: ErrorCodes.PAYLOAD_TOO_LARGE,
    traceId: opts.traceId,
  });
}

/** HTTP 429 / `RATE_LIMITED` (override-able). */
export function tooManyRequests<T = void>(opts: CodedOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.TOO_MANY_REQUESTS],
    statusCode: HttpStatusCode.TooManyRequests,
    errorCode: opts.errorCode ?? ErrorCodes.RATE_LIMITED,
    traceId: opts.traceId,
  });
}

/** HTTP 400 / `CANCELED`. */
export function canceled<T = void>(opts: BasicOpts = {}): D2Result<T> {
  return new D2Result<T>({
    success: false,
    messages: opts.messages ?? [DEFAULTS.CANCELED],
    statusCode: HttpStatusCode.BadRequest,
    errorCode: ErrorCodes.CANCELED,
    traceId: opts.traceId,
  });
}

/**
 * HTTP 206 / `SOME_FOUND`. Partial-success on the
 * NOT_FOUND → SOME_FOUND → OK ladder; `success` is `false` because the
 * query did not return all requested items.
 */
export function someFound<T = void>(
  opts: BasicOpts & { data?: T } = {},
): D2Result<T> {
  return new D2Result<T>({
    success: false,
    data: opts.data,
    messages: opts.messages ?? [DEFAULTS.SOME_FOUND],
    statusCode: HttpStatusCode.PartialContent,
    errorCode: ErrorCodes.SOME_FOUND,
    traceId: opts.traceId,
  });
}

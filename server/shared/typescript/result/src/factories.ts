// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { TK } from "@d2/i18n-keys";
import { ErrorCategoryWire } from "@d2/error-category";

import { D2Result } from "./d2-result.js";
import { ErrorCodes } from "./error-codes.g.js";
import type { BasicOpts, ValidationFailedOpts } from "./factories.g.js";
import { HttpStatusCode } from "./http-status-codes.js";

// The 10 spec-derived semantic FAILURE factories (notFound / unauthorized /
// forbidden / validationFailed / conflict / serviceUnavailable /
// unhandledException / payloadTooLarge / tooManyRequests / canceled) are
// codegen-generated into `./factories.g.ts` from
// `contracts/error-codes/error-codes.spec.json` — the same spec drives the
// .NET `D2Result` base factories, so the cross-language wire stays
// byte-identical. The factories below are NOT spec-derived: `ok` / `created`
// (success/status), `fail` (arbitrary status + code), `someFound` (206,
// data-carrying) — they stay hand-rolled and re-exported alongside the
// generated set from `./index.ts`.

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
    messages: opts.messages ?? [TK.common.errors.SOME_FOUND],
    statusCode: HttpStatusCode.PartialContent,
    errorCode: ErrorCodes.SOME_FOUND,
    traceId: opts.traceId,
    category: ErrorCategoryWire.PartialSuccess,
  });
}

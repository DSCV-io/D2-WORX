// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { HttpStatusCode } from "./http-status-codes.js";
import type { InputError } from "./input-error.js";
import type { ErrorCategory } from "@d2/error-category";
import type { TKMessage } from "@d2/i18n-abstractions";

/**
 * Constructor input for `D2Result`. All optional except `success`.
 */
export interface D2ResultInit<T = void> {
  readonly success: boolean;
  readonly data?: T;
  readonly messages?: readonly TKMessage[];
  readonly inputErrors?: readonly InputError[];
  readonly statusCode?: HttpStatusCode;
  readonly errorCode?: string;
  readonly traceId?: string;
  readonly category?: ErrorCategory;
}

/**
 * Result of an operation. Mirrors .NET `D2.Shared.Result.D2Result` /
 * `D2Result<T>` — same field shape so cross-language wire is byte-identical.
 *
 * Producers should always prefer a semantic factory
 * (`D2Result.notFound()`, `D2Result.unauthorized()`, etc.) over raw `fail()`
 * when one matches the failure mode — semantic factories carry the canonical
 * status code, error code, and default message together.
 */
export class D2Result<T = void> {
  readonly success: boolean;
  readonly data: T | undefined;
  readonly messages: readonly TKMessage[];
  readonly inputErrors: readonly InputError[];
  readonly statusCode: HttpStatusCode;
  readonly errorCode: string | undefined;
  readonly traceId: string | undefined;
  readonly category: ErrorCategory | undefined;

  constructor(init: D2ResultInit<T>) {
    this.success = init.success;
    this.data = init.data;
    this.messages = init.messages ?? [];
    this.inputErrors = init.inputErrors ?? [];
    this.statusCode =
      init.statusCode ??
      (init.success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
    this.errorCode = init.errorCode;
    this.traceId = init.traceId;
    this.category = init.category;
  }

  /** True when the operation failed. */
  get failed(): boolean {
    return !this.success;
  }

  /** True when the result represents partial success (HTTP 206 / SOME_FOUND). */
  get isPartialSuccess(): boolean {
    return this.statusCode === HttpStatusCode.PartialContent;
  }

  /**
   * Returns a new `D2Result` with the same shape but `traceId` swapped.
   * Used by the handler pipeline to auto-inject the request trace id on
   * results that cross the handler boundary.
   */
  withTraceId(traceId: string | undefined): D2Result<T> {
    return new D2Result<T>({
      success: this.success,
      data: this.data,
      messages: this.messages,
      inputErrors: this.inputErrors,
      statusCode: this.statusCode,
      errorCode: this.errorCode,
      traceId,
      category: this.category,
    });
  }
}

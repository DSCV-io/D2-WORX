import { ErrorCodes, type ErrorCode } from "./error-codes.js";
import { HttpStatusCode } from "./http-status-codes.js";

/**
 * Input validation error: [fieldName, ...errorMessages].
 * First element is the field name, remaining elements are error messages.
 */
export type InputError = [field: string, ...errors: string[]];

/**
 * Options for constructing a D2Result.
 */
export interface D2ResultOptions<TData = void> {
  success: boolean;
  data?: TData;
  messages?: string[];
  inputErrors?: InputError[];
  statusCode?: HttpStatusCode;
  errorCode?: ErrorCode | string;
  traceId?: string;
}

/**
 * Standardized result type for operation outcomes.
 * Mirrors D2.Shared.Result.D2Result in .NET.
 *
 * Use static factory methods (ok, fail, notFound, etc.) instead of the constructor.
 *
 * Default messages in failure factories are TK translation key strings (e.g.
 * "common_errors_NOT_FOUND") rather than English prose. The translation middleware
 * resolves these keys to locale-appropriate text before they reach the client.
 * Keys are hardcoded here instead of imported from @d2/i18n to keep @d2/result
 * a zero-dependency foundational package.
 */
export class D2Result<TData = void> {
  readonly success: boolean;
  readonly data: TData | undefined;
  readonly messages: readonly string[];
  readonly inputErrors: readonly InputError[];
  readonly statusCode: HttpStatusCode;
  readonly errorCode: ErrorCode | string | undefined;
  readonly traceId: string | undefined;

  constructor(options: D2ResultOptions<TData>) {
    this.success = options.success;
    this.data = options.data;
    this.messages = Object.freeze(options.messages ?? []);
    this.inputErrors = Object.freeze(options.inputErrors ?? []);
    this.statusCode =
      options.statusCode ?? (options.success ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
    this.errorCode = options.errorCode;
    this.traceId = options.traceId;
  }

  /** True if the result represents a failure. */
  get failed(): boolean {
    return !this.success;
  }

  /**
   * Check if the result is successful and extract the data.
   * Returns the data if successful, undefined otherwise.
   */
  checkSuccess(): TData | undefined {
    return this.success ? this.data : undefined;
  }

  /**
   * Check if the result is a failure and extract partial data (if any).
   * Returns the data if failed (may be partial, e.g. SOME_FOUND), undefined if success.
   */
  checkFailure(): TData | undefined {
    return this.failed ? this.data : undefined;
  }

  // ---------------------------------------------------------------------------
  // Success factories
  // ---------------------------------------------------------------------------

  /** Create a successful result. */
  static ok<T = void>(options?: { data?: T; messages?: string[]; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: true,
      data: options?.data,
      messages: options?.messages,
      statusCode: HttpStatusCode.OK,
      traceId: options?.traceId,
    });
  }

  /** Create a successful result with 201 Created status. */
  static created<T = void>(options?: { data?: T; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: true,
      data: options?.data,
      statusCode: HttpStatusCode.Created,
      traceId: options?.traceId,
    });
  }

  // ---------------------------------------------------------------------------
  // Failure factories
  // ---------------------------------------------------------------------------

  /** Create a general failure result. */
  static fail<T = void>(options?: {
    messages?: string[];
    statusCode?: HttpStatusCode;
    inputErrors?: InputError[];
    errorCode?: ErrorCode | string;
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages,
      statusCode: options?.statusCode,
      inputErrors: options?.inputErrors,
      errorCode: options?.errorCode,
      traceId: options?.traceId,
    });
  }

  /** Create a 404 Not Found result. */
  static notFound<T = void>(options?: { messages?: string[]; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_NOT_FOUND"],
      statusCode: HttpStatusCode.NotFound,
      errorCode: ErrorCodes.NOT_FOUND,
      traceId: options?.traceId,
    });
  }

  /** Create a 401 Unauthorized result. */
  static unauthorized<T = void>(options?: { messages?: string[]; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_UNAUTHORIZED"],
      statusCode: HttpStatusCode.Unauthorized,
      errorCode: ErrorCodes.UNAUTHORIZED,
      traceId: options?.traceId,
    });
  }

  /** Create a 403 Forbidden result. */
  static forbidden<T = void>(options?: { messages?: string[]; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_FORBIDDEN"],
      statusCode: HttpStatusCode.Forbidden,
      errorCode: ErrorCodes.FORBIDDEN,
      traceId: options?.traceId,
    });
  }

  /**
   * Create a 400 Validation Failed result.
   *
   * The optional `errorCode` overrides the default `VALIDATION_FAILED` so
   * callers can attach a more specific code (e.g. `PHONE_NO_CHANGE`,
   * `FILES_INVALID_CONTENT_TYPE`) for client-side discrimination — without
   * dropping back to raw `D2Result.fail()`.
   */
  static validationFailed<T = void>(options?: {
    messages?: string[];
    inputErrors?: InputError[];
    errorCode?: ErrorCode | string;
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_VALIDATION_FAILED"],
      inputErrors: options?.inputErrors,
      statusCode: HttpStatusCode.BadRequest,
      errorCode: options?.errorCode ?? ErrorCodes.VALIDATION_FAILED,
      traceId: options?.traceId,
    });
  }

  /** Create a 409 Conflict result. */
  static conflict<T = void>(options?: { messages?: string[]; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_CONFLICT"],
      statusCode: HttpStatusCode.Conflict,
      errorCode: ErrorCodes.CONFLICT,
      traceId: options?.traceId,
    });
  }

  /** Create a 503 Service Unavailable result. */
  static serviceUnavailable<T = void>(options?: {
    messages?: string[];
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_SERVICE_UNAVAILABLE"],
      statusCode: HttpStatusCode.ServiceUnavailable,
      errorCode: ErrorCodes.SERVICE_UNAVAILABLE,
      traceId: options?.traceId,
    });
  }

  /** Create a 500 Unhandled Exception result. */
  static unhandledException<T = void>(options?: {
    messages?: string[];
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_unknown"],
      statusCode: HttpStatusCode.InternalServerError,
      errorCode: ErrorCodes.UNHANDLED_EXCEPTION,
      traceId: options?.traceId,
    });
  }

  /** Create a 413 Payload Too Large result. */
  static payloadTooLarge<T = void>(options?: {
    messages?: string[];
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_PAYLOAD_TOO_LARGE"],
      statusCode: HttpStatusCode.RequestEntityTooLarge,
      errorCode: ErrorCodes.PAYLOAD_TOO_LARGE,
      traceId: options?.traceId,
    });
  }

  /**
   * Create a 429 Too Many Requests result (rate limited).
   *
   * The optional `errorCode` overrides the default `RATE_LIMITED` so callers
   * can attach a more specific code (e.g. `OTP_RATE_LIMITED`) for client-side
   * discrimination.
   */
  static tooManyRequests<T = void>(options?: {
    messages?: string[];
    errorCode?: ErrorCode | string;
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_TOO_MANY_REQUESTS"],
      statusCode: HttpStatusCode.TooManyRequests,
      errorCode: options?.errorCode ?? ErrorCodes.RATE_LIMITED,
      traceId: options?.traceId,
    });
  }

  /** Create a cancelled result (client or server cancellation). */
  static cancelled<T = void>(options?: { messages?: string[]; traceId?: string }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: options?.messages ?? ["common_errors_CANCELLED"],
      statusCode: HttpStatusCode.BadRequest,
      errorCode: ErrorCodes.CANCELLED,
      traceId: options?.traceId,
    });
  }

  // ---------------------------------------------------------------------------
  // Partial success
  // ---------------------------------------------------------------------------

  /**
   * Create a 206 Partial Content result (some items found, but not all).
   * Marked as failure but includes data.
   */
  static someFound<T = void>(options?: {
    data?: T;
    messages?: string[];
    traceId?: string;
  }): D2Result<T> {
    return new D2Result<T>({
      success: false,
      data: options?.data,
      messages: options?.messages,
      statusCode: HttpStatusCode.PartialContent,
      errorCode: ErrorCodes.SOME_FOUND,
      traceId: options?.traceId,
    });
  }

  // ---------------------------------------------------------------------------
  // Bubbling (propagate errors with type change)
  // ---------------------------------------------------------------------------

  /**
   * Propagate a failure result with a different data type.
   * Preserves all error details, sets data to undefined.
   */
  static bubbleFail<T>(source: D2Result<unknown>): D2Result<T> {
    return new D2Result<T>({
      success: false,
      messages: [...source.messages],
      inputErrors: source.inputErrors.map((ie) => [...ie]),
      statusCode: source.statusCode,
      errorCode: source.errorCode,
      traceId: source.traceId,
    });
  }

  /**
   * Convert a result to a different data type, preserving all details.
   * Optionally provide new data.
   */
  static bubble<T>(source: D2Result<unknown>, data?: T): D2Result<T> {
    return new D2Result<T>({
      success: source.success,
      data,
      messages: [...source.messages],
      inputErrors: source.inputErrors.map((ie) => [...ie]),
      statusCode: source.statusCode,
      errorCode: source.errorCode,
      traceId: source.traceId,
    });
  }
}

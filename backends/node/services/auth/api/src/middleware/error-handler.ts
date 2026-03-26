import type { Context } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { SpanStatusCode } from "@opentelemetry/api";
import { getServerSpan } from "@d2/service-defaults";
import { D2Result, HttpStatusCode } from "@d2/result";
import { AuthValidationError } from "@d2/auth-domain";
import { TK } from "@d2/i18n";
import type { ILogger } from "@d2/logging";

/**
 * Creates a global error handler that catches unhandled exceptions and returns
 * a D2Result-shaped JSON response.
 *
 * Security: Only surfaces error details for known validation errors (4xx).
 * For 5xx/unknown errors, returns a generic message to prevent information leakage
 * (SQL details, file paths, internal state).
 */
export function createErrorHandler(logger: ILogger) {
  return function handleError(err: Error, c: Context): Response {
    // Record error on the active OTel span (no-op if no provider registered).
    const span = getServerSpan(c);
    if (span) {
      span.recordException(err);
      span.setStatus({ code: SpanStatusCode.ERROR, message: err.message });
    }

    const rawStatus = "status" in err && typeof err.status === "number" ? err.status : 500;

    // For validation errors, provide structured error details
    if (err instanceof AuthValidationError) {
      const result = D2Result.validationFailed();
      return c.json(result, 400 as ContentfulStatusCode);
    }

    // For 4xx client errors, provide a safe message
    if (rawStatus >= 400 && rawStatus < 500) {
      const result = D2Result.fail({
        messages: [safeClientMessage(rawStatus)],
        statusCode: rawStatus as HttpStatusCode,
      });
      return c.json(result, rawStatus as ContentfulStatusCode);
    }

    // For 5xx server errors, log with structured logger, NEVER leak err.message to client
    logger.error("Unhandled server error", {
      error_message: err.message,
      error_stack: err.stack,
      url: c.req.url,
      method: c.req.method,
    });
    const result = D2Result.unhandledException();
    return c.json(result, 500 as ContentfulStatusCode);
  };
}

function safeClientMessage(status: number): string {
  switch (status) {
    case 400:
      return TK.common.errors.BAD_REQUEST;
    case 401:
      return TK.common.errors.UNAUTHORIZED;
    case 403:
      return TK.common.errors.FORBIDDEN;
    case 404:
      return TK.common.errors.NOT_FOUND;
    case 409:
      return TK.common.errors.CONFLICT;
    case 429:
      return TK.common.errors.TOO_MANY_REQUESTS;
    default:
      return TK.common.errors.REQUEST_FAILED;
  }
}

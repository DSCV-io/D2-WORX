/**
 * Gateway response parser — converts fetch Response into D2Result.
 *
 * The .NET gateway has an inconsistent serialization issue:
 * - Endpoint responses use PascalCase: { "Success": true, "Data": {...} }
 * - Middleware errors use camelCase: { "success": false, "messages": [...] }
 *
 * Rather than checking both casings per-property, we normalize all JSON keys
 * to camelCase in a single pass before reading properties. HTTP status is
 * used as the authoritative status code (avoids int vs string body inconsistency).
 *
 * Isomorphic — works in both server (Node.js) and browser environments.
 */
import { TK } from "@d2/i18n/keys";
import { D2Result, type HttpStatusCode, type InputError } from "@d2/result";

/**
 * Shape of a gateway D2Result body after camelCase normalization.
 * Only the known D2Result envelope properties — `data` is generic.
 */
interface NormalizedBody {
  success?: boolean;
  data?: unknown;
  messages?: string[];
  inputErrors?: InputError[];
  errorCode?: string;
  traceId?: string;
  // statusCode from body is ignored — we use HTTP status instead
}

/**
 * Convert a single PascalCase key to camelCase.
 * "Success" → "success", "StatusCode" → "statusCode", "already" → "already"
 */
function toCamelCase(key: string): string {
  if (!key) return key;
  // Find the leading uppercase run and lowercase it
  // "ID" → "id", "IPAddress" → "ipAddress", "StatusCode" → "statusCode"
  let i = 0;
  while (i < key.length && key[i] === key[i].toUpperCase() && key[i] !== key[i].toLowerCase()) {
    i++;
  }
  if (i === 0) return key; // already starts lowercase
  if (i === 1) return key[0].toLowerCase() + key.slice(1); // "Success" → "success"
  if (i === key.length) return key.toLowerCase(); // All caps: "ID" → "id"
  // Multi-char uppercase prefix before lowercase: "IPAddress" → "ipAddress"
  return key.slice(0, i - 1).toLowerCase() + key.slice(i - 1);
}

/**
 * Recursively normalize all object keys to camelCase.
 * Arrays are traversed, primitives pass through unchanged.
 */
export function normalizeKeys<T = unknown>(value: unknown): T {
  if (value === null || value === undefined) return value as T;
  if (Array.isArray(value)) return value.map((item) => normalizeKeys(item)) as T;
  if (typeof value === "object") {
    const result: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value)) {
      result[toCamelCase(key)] = normalizeKeys(val);
    }
    return result as T;
  }
  return value as T;
}

/**
 * Parse a gateway fetch Response into a typed D2Result.
 *
 * - Uses `response.status` as the authoritative HTTP status code.
 * - Normalizes all body keys to camelCase (handles both PascalCase and camelCase).
 * - Non-JSON responses are wrapped in a fail result with the body text as message.
 */
export async function parseGatewayResponse<TData = void>(
  response: Response,
): Promise<D2Result<TData>> {
  const statusCode = response.status as HttpStatusCode;

  let text: string;
  try {
    text = await response.text();
  } catch {
    return new D2Result<TData>({
      success: response.ok,
      statusCode,
      messages: [response.statusText || TK.common.errors.REQUEST_FAILED],
    });
  }

  if (!text.trim()) {
    return new D2Result<TData>({
      success: response.ok,
      statusCode,
    });
  }

  let body: NormalizedBody;
  try {
    const raw = JSON.parse(text);
    body = normalizeKeys<NormalizedBody>(raw);
  } catch {
    // Non-JSON response (e.g. plain text error page)
    return new D2Result<TData>({
      success: false,
      statusCode,
      messages: [text],
    });
  }

  return new D2Result<TData>({
    success: body.success ?? response.ok,
    data: body.data as TData | undefined,
    messages: body.messages,
    inputErrors: body.inputErrors,
    errorCode: body.errorCode,
    traceId: body.traceId,
    statusCode,
  });
}

/**
 * Create a D2Result for a network-level error (fetch threw).
 */
export function networkErrorResult<TData = void>(error: unknown): D2Result<TData> {
  const message = error instanceof Error ? error.message : TK.common.errors.REQUEST_FAILED;

  return D2Result.unhandledException<TData>({ messages: [message] });
}

/**
 * Options for `executeFetch` — the shared fetch+timeout+error wrapper.
 */
export interface ExecuteFetchOptions {
  method?: string;
  headers: Headers;
  body?: string;
  signal?: AbortSignal;
  timeout?: number;
  credentials?: RequestCredentials;
}

/**
 * Execute a fetch with timeout handling, abort support, and D2Result error mapping.
 *
 * Shared by all three gateway clients (server gateway, client gateway, auth gateway)
 * to eliminate the triplicated timeout/abort/error catch blocks.
 */
export async function executeFetch<TData>(
  url: string,
  options: ExecuteFetchOptions,
): Promise<D2Result<TData>> {
  const timeoutMs = options.timeout ?? 10_000;

  try {
    const timeoutSignal = AbortSignal.timeout(timeoutMs);
    const signal = options.signal
      ? AbortSignal.any([options.signal, timeoutSignal])
      : timeoutSignal;

    const response = await fetch(url, {
      method: options.method ?? "GET",
      headers: options.headers,
      body: options.body,
      signal,
      credentials: options.credentials,
    });

    return parseGatewayResponse<TData>(response);
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === "AbortError") {
      return D2Result.fail<TData>({
        messages: [TK.common.errors.CANCELLED],
        statusCode: 408 as HttpStatusCode,
      });
    }

    if (error instanceof DOMException && error.name === "TimeoutError") {
      return D2Result.fail<TData>({
        messages: [TK.common.errors.REQUEST_FAILED],
        statusCode: 408 as HttpStatusCode,
      });
    }

    return networkErrorResult<TData>(error);
  }
}

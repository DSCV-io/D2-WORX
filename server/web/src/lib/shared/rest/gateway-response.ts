// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Gateway response parser — converts fetch Response into D2Result.
 *
 * The .NET gateway emits the D2Result Shape B envelope with canonical
 * camelCase property names — pinned via [JsonPropertyName(...)] attributes
 * on D2Result's properties that reference the spec-driven
 * D2ResultEnvelopeFieldNames constants (single source of truth across
 * .NET + TS, see contracts/d2result-envelope/d2result-envelope.spec.json).
 * Cross-language wire drift on the 7 envelope field names is structurally
 * impossible.
 *
 * The `normalizeKeys()` shim provides defense-in-depth for two scenarios:
 * 1. Non-D2Result endpoints (e.g. ASP.NET Core health-check JSON,
 *    OpenAPI metadata) that don't use the D2Result serializer + don't
 *    carry [JsonPropertyName] attributes — those may render as PascalCase
 *    depending on the calling endpoint's JsonSerializerOptions.
 * 2. Test fixtures that record PascalCase response bodies — the parser
 *    stays tolerant so fixture-driven tests pass without regenerating
 *    every fixture.
 * The shim is not required for D2Result-driven endpoints (the spec-driven
 * envelope keys ship as camelCase unconditionally); HTTP status is used as
 * the authoritative status code (avoids int-vs-string body inconsistency).
 *
 * Isomorphic — works in both server (Node.js) and browser environments.
 */
import { TK } from "@d2/i18n/keys";
import {
  D2Result,
  D2ResultEnvelopeFieldNames,
  type HttpStatusCode,
  type InputError,
  type TKMessage,
  tk,
} from "@d2/result";

/**
 * Shape of a gateway D2Result body after camelCase normalization.
 * Only the known D2Result envelope properties — `data` is generic.
 *
 * Keys match the spec-derived D2ResultEnvelopeFieldNames catalog
 * (`success`, `data`, `messages`, `inputErrors`, `errorCode`, `traceId`,
 * `statusCode`). The `KeysMatchSpec` const at the bottom of this file
 * enforces that the TypeScript property names here track the wire
 * constants — a TS compile error fires the moment one drifts.
 *
 * `messages` is typed as `TKMessage[]` to match the .NET wire format
 * (the .NET `D2Result.Messages` is `IReadOnlyList<TKMessage>` serialized
 * via `TKMessageJsonConverter` as `[{key, params?}, ...]`). Runtime
 * values arrive as TKMessage objects from the gateway, so the static type
 * matches the wire shape exactly. Consumers needing rendered strings call
 * `renderMessages` from `@d2/result` at the BFF/browser boundary.
 */
interface NormalizedBody {
  success?: boolean;
  data?: unknown;
  messages?: TKMessage[];
  inputErrors?: InputError[];
  errorCode?: string;
  traceId?: string;
  // statusCode from body is ignored — we use HTTP status instead.
  statusCode?: number;
}

// Compile-time pin: the NormalizedBody key set MUST be exactly the wire
// values in D2ResultEnvelopeFieldNames. If either side drifts (spec adds
// a field; NormalizedBody renames or drops one), this assertion errors
// at TypeScript compile time naming the offending key.
type _NormalizedBodyKeys = keyof NormalizedBody;
type _SpecFieldValues =
  (typeof D2ResultEnvelopeFieldNames)[keyof typeof D2ResultEnvelopeFieldNames];
type _Assert<T extends true> = T;
type _SpecCoversBody = Exclude<_NormalizedBodyKeys, _SpecFieldValues>;
type _BodyCoversSpec = Exclude<_SpecFieldValues, _NormalizedBodyKeys>;
// Both must be `never` — bidirectional cover.
type _ParityCheck = _Assert<
  [_SpecCoversBody] extends [never]
    ? [_BodyCoversSpec] extends [never]
      ? true
      : false
    : false
>;
// Touch the alias so unused-type-checker doesn't flag it.
const _PARITY_PIN: _ParityCheck = true;
void _PARITY_PIN;

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
      messages: [tk(TK.common.errors.REQUEST_FAILED)],
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
    // Non-JSON response (e.g. plain text error page). Wrap in a synthetic
    // TKMessage carrying REQUEST_FAILED — the raw text isn't a translation
    // key, so we don't pass it through as if it were. Consumers needing
    // the original body for diagnostics can read it via the trace id.
    return new D2Result<TData>({
      success: false,
      statusCode,
      messages: [tk(TK.common.errors.REQUEST_FAILED)],
    });
  }

  // Read via the spec-driven D2ResultEnvelopeFieldNames constants — the
  // body shape post-normalizeKeys() exposes camelCase keys identical to
  // the wire field names; index by the constants so a spec rename would
  // surface as a TS compile error (the constants type narrows the index
  // signature).
  const bodyMap = body as unknown as Record<string, unknown>;
  return new D2Result<TData>({
    success:
      (bodyMap[D2ResultEnvelopeFieldNames.SUCCESS] as boolean | undefined) ??
      response.ok,
    data: bodyMap[D2ResultEnvelopeFieldNames.DATA] as TData | undefined,
    messages: bodyMap[D2ResultEnvelopeFieldNames.MESSAGES] as
      | TKMessage[]
      | undefined,
    inputErrors: bodyMap[D2ResultEnvelopeFieldNames.INPUT_ERRORS] as
      | InputError[]
      | undefined,
    errorCode: bodyMap[D2ResultEnvelopeFieldNames.ERROR_CODE] as
      | string
      | undefined,
    traceId: bodyMap[D2ResultEnvelopeFieldNames.TRACE_ID] as string | undefined,
    statusCode,
  });
}

/**
 * Create a D2Result for a network-level error (fetch threw). The runtime
 * `error.message` (when present) is unstructured English from the
 * underlying fetch / DOM exception — it does NOT carry an i18n key, so
 * we always ship `REQUEST_FAILED` as the TKMessage and surface the raw
 * exception detail via the trace id / log pipeline (NOT through the
 * `messages` field which is reserved for translation-key envelopes).
 */
export function networkErrorResult<TData = void>(_error: unknown): D2Result<TData> {
  return D2Result.unhandledException<TData>({
    messages: [tk(TK.common.errors.REQUEST_FAILED)],
  });
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
        messages: [tk(TK.common.errors.CANCELED)],
        statusCode: 408 as HttpStatusCode,
      });
    }

    if (error instanceof DOMException && error.name === "TimeoutError") {
      return D2Result.fail<TData>({
        messages: [tk(TK.common.errors.REQUEST_FAILED)],
        statusCode: 408 as HttpStatusCode,
      });
    }

    return networkErrorResult<TData>(error);
  }
}

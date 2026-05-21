<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/result

> Parent: [`server/shared/typescript/`](../README.md)

`D2Result<T>` + semantic factories + combine/bubble helpers.
Mirrors `D2.Shared.Result` (.NET) so the cross-language wire is byte-identical.

## Public API

| Export                                                         | Purpose                                                                                                     |
| -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `D2Result<T>` (class)                                          | Result of an operation: `success`, `data`, `messages`, `inputErrors`, `statusCode`, `errorCode`, `traceId`. |
| `HttpStatusCode`                                               | Numeric status constants used by the result layer.                                                          |
| `ErrorCodes` / `ErrorCode`                                     | Standardized `errorCode` string constants. Codegen-emitted from `contracts/error-codes/error-codes.spec.json` via `tools/ts-codegen` — same spec drives the .NET-side `D2.Shared.Result.ErrorCodes` so the cross-language wire stays byte-identical (cross-language parity-tested in `@d2/contract-tests`). |
| `ALL_ERROR_CODES`                                              | Spec-order enumeration of every code (useful for telemetry tag-value membership checks).                    |
| `getErrorHttpStatus(code)`                                     | Returns the spec-declared HTTP status for a code (`500` for unknown codes — defensive default).             |
| `TKMessage` / `tk(key, params?)`                               | Translation-key message shape + helper. Wire shape pinned by `TkMessageWireShape` (codegen-emitted from `contracts/tk-message/tk-message.spec.json`); same spec drives the .NET `D2.Shared.I18n.TkMessageWireShape` catalog. |
| `TkMessageWireShape`                                           | `{KEY: "key", PARAMS: "params"} as const` — codegen-emitted property-name constants governing the wire shape. Use these constants when serializing / parsing TKMessage envelopes by hand. Cross-language parity-tested. |
| `InputError` / `inputError(field, errors)`                     | Per-field validation error wire shape + helper. Wire shape pinned by `InputErrorWireShape`; same spec drives the .NET `D2.Shared.Result.InputErrorWireShape`. The shape is an OBJECT `{field, errors: TKMessage[]}` — NOT a tuple. |
| `InputErrorWireShape`                                          | `{FIELD: "field", ERRORS: "errors"} as const` — codegen-emitted property-name constants. Cross-language parity-tested. |
| `D2ResultEnvelopeFieldNames`                                   | `{SUCCESS, DATA, MESSAGES, INPUT_ERRORS, ERROR_CODE, TRACE_ID, STATUS_CODE} as const` — codegen-emitted catalog of the D2Result Shape B wire envelope's 7 property names. Mirrors `D2.Shared.Result.D2ResultEnvelopeFieldNames` byte-for-byte (single source: `contracts/d2result-envelope/d2result-envelope.spec.json`). Use these constants in BFF gateway parsers / wire-shape assertions / fixtures instead of hand-rolling `"success"` / `"messages"` string literals. Cross-language parity-tested. |
| `ALL_D2RESULT_ENVELOPE_FIELD_NAMES`                            | Spec-order enumeration of every envelope field name (catalog-pin guards). |
| `renderMessage(message, translate)`                            | Boundary helper — renders a single `TKMessage` to a localized string via a translator function. Use at the BFF / browser boundary where consumers (toasts, form display) expect rendered text. |
| `renderMessages(messages, translate)`                          | Boundary helper — renders a `TKMessage[]` to `string[]`. Null/undefined/empty input returns `[]`. |
| `renderInputErrors(inputErrors, translate)`                    | Boundary helper — renders an `InputError[]` to a `field → string[]` map. Suitable for Superforms-style form-error display. |
| `TranslateFn`                                                  | `(key: string, params?: Record<string, unknown>) => string` — signature for translator functions passed to the render helpers. Implementations typically wrap Paraglide's `m[key](params)` keyed lookup. |
| `ok()` / `created()` / `fail()`                                | Basic success/failure factories.                                                                            |
| `notFound()` / `unauthorized()` / `forbidden()`                | 404 / 401 / 403 semantic factories.                                                                         |
| `validationFailed()`                                           | 400 + per-field input errors.                                                                               |
| `conflict()` / `serviceUnavailable()` / `unhandledException()` | 409 / 503 / 500.                                                                                            |
| `payloadTooLarge()` / `tooManyRequests()` / `canceled()`       | 413 / 429 / 400-canceled.                                                                                   |
| `someFound()`                                                  | 206 partial-success on the NOT_FOUND → SOME_FOUND → OK ladder.                                              |
| `combine(...)` / `combineMany(iter)`                           | Aggregate 2-5 results into a tuple-typed result; iterable variant.                                          |
| `bubble(src, data?)` / `bubbleFail(src)`                       | Re-shape a typed downstream result while preserving fail metadata.                                          |

## Dependencies

- `@d2/utilities` (workspace internal — boundary helpers)

## Usage example

```ts
import { ok, notFound, validationFailed, combine } from "@d2/result";

function getUser(id: string) {
  if (!id)
    return validationFailed({
      inputErrors: [
        { field: "id", errors: [{ key: "TK.Common.Validation.REQUIRED" }] },
      ],
    });
  const row = await db.users.find(id);
  if (!row) return notFound<User>();
  return ok(mapRow(row));
}

const tuple = combine(getUser("a"), getUser("b"));
if (tuple.failed) return tuple; // bubble
const [u1, u2] = tuple.data!;
```

## Parity with .NET

Mirrors `D2.Shared.Result`:

- `D2Result<T>` ↔ `D2Result<TData>` — same fields, same wire shape.
- Semantic factories ↔ `D2Result.{Ok, Created, Fail, NotFound, Unauthorized, Forbidden, ValidationFailed, Conflict, ServiceUnavailable, UnhandledException, PayloadTooLarge, TooManyRequests, Canceled, SomeFound}`.
- `combine` / `combineMany` ↔ `D2Result.Combine` (5-arity overloads + IEnumerable).
- `bubble` / `bubbleFail` ↔ `D2Result.Bubble` / `BubbleFail` extension methods.
- `ErrorCodes` / `ALL_ERROR_CODES` / `getErrorHttpStatus` ↔ `D2.Shared.Result.ErrorCodes` static (single spec source emits both sides; cross-language parity tested in `@d2/contract-tests` at `tests/error-codes.parity.test.ts`).
- `TkMessageWireShape` ↔ `D2.Shared.I18n.TkMessageWireShape` static (single spec source; parity-tested at `tests/tk-message.parity.test.ts` including round-trip byte-equal fixtures produced by `TKMessageJsonConverter`).
- `InputErrorWireShape` ↔ `D2.Shared.Result.InputErrorWireShape` static (single spec source; parity-tested at `tests/input-error.parity.test.ts` including nested TKMessage round-trip fixtures).
- `renderMessage` / `renderMessages` / `renderInputErrors` are TS-only — the .NET-side equivalent is `ITranslator` (outbound notifications path), not used inline at serialization boundaries because .NET ships TKMessage shapes unchanged on the HTTP wire path. See [`docs/PATTERNS.md`](../../../../docs/PATTERNS.md) "BFF boundary translation" section.

## Edge cases

- `bubbleFail` throws on success input (programming-error guard).
- `combineMany([])` returns `ok([])` — no-op aggregation succeeds.
- `someFound()` carries `success: false` (partial-success ladder semantics).
- Default messages use string-literal TK keys aligned with .NET `TK.Common.Errors.*`.

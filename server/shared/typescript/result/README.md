<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/result

`D2Result<T>` + semantic factories + combine/bubble helpers.
Mirrors `D2.Shared.Result` (.NET) so the cross-language wire is byte-identical.

## Public API

| Export                                                         | Purpose                                                                                                     |
| -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `D2Result<T>` (class)                                          | Result of an operation: `success`, `data`, `messages`, `inputErrors`, `statusCode`, `errorCode`, `traceId`. |
| `HttpStatusCode`                                               | Numeric status constants used by the result layer.                                                          |
| `ErrorCodes` / `ErrorCode`                                     | Standardized `errorCode` string constants.                                                                  |
| `TKMessage` / `tk(key, params?)`                               | Translation-key message shape + helper.                                                                     |
| `InputError` / `inputError(field, errors)`                     | Per-field validation error wire shape + helper.                                                             |
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
- `ErrorCodes` ↔ `D2.Shared.Result.ErrorCodes` static (same string values).

## Edge cases

- `bubbleFail` throws on success input (programming-error guard).
- `combineMany([])` returns `ok([])` — no-op aggregation succeeds.
- `someFound()` carries `success: false` (partial-success ladder semantics).
- Default messages use string-literal TK keys aligned with .NET `TK.Common.Errors.*`.

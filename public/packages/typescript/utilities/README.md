<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# @dcsv-io/d2-utilities

> Parent: [`public/packages/typescript/`](../README.md)

Boundary helpers — `falsey`/`truthy` semantics, string cleaning,
parse-or-undefined helpers, indexed env-var array parsing, and the regex
constants used across the TS codebase. Mirrors `DcsvIo.D2.Utilities` (.NET).

## Public API

| Export                                                                              | Purpose                                                                                                                   |
| ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `falsey(value)`                                                                     | True when value is null/undefined/empty/whitespace string/empty collection.                                               |
| `truthy(value)`                                                                     | Inverse of `falsey`.                                                                                                      |
| `toUndefIfEmpty(s)`                                                                 | Returns trimmed string or `undefined` when empty/whitespace.                                                              |
| `cleanStr(s)`                                                                       | Trims + collapses whitespace runs to one space; `undefined` when empty.                                                   |
| `cleanDisplayStr(s)`                                                                | Strips chars not in display-name allowlist + `cleanStr`.                                                                  |
| `tryParseTruthyUndefUuid(s)`                                                        | Canonical lowercase UUID on success; `undefined` otherwise (empty UUID → undefined).                                      |
| `tryParseTruthyUndefInt(s)`                                                         | Parsed integer on success; `undefined` otherwise.                                                                         |
| `tryParseTruthyUndefEnum(enumObj, s)`                                               | Canonical key on case-insensitive match; `undefined` otherwise.                                                           |
| `chunk(arr, size)`                                                                  | Splits into consecutive chunks of `size`; throws on `size < 1`.                                                           |
| `clean(items, cleaner, opts?)`                                                      | Applies a per-element cleaner to any `Iterable<T>`; `opts` chooses null/empty handling — defaults match the .NET sibling. |
| `parseEnvArray(prefix, env)`                                                        | Reads `PREFIX__0=a, PREFIX__1=b, ...` indexed env-var arrays; stops at first gap.                                         |
| `uuidv7(now?)`                                                                      | Mints a time-ordered RFC 9562 UUIDv7 (48-bit ms-timestamp prefix + random bits); optional injectable clock for tests.     |
| `WHITESPACE_RE` / `DISPLAY_NAME_INVALID_RE` / `EMAIL_RE` / `UUID_RE` / `EMPTY_UUID` | Pre-built regex + canonical empty-UUID constants.                                                                         |

## Dependencies

None. Zero runtime deps.

## Usage example

```ts
import { falsey, tryParseTruthyUndefUuid, parseEnvArray } from "@dcsv-io/d2-utilities";

if (falsey(input)) return undefined;

const id = tryParseTruthyUndefUuid(headers["x-org-id"]);
if (id === undefined) throw new Error("missing org id");

const audiences = parseEnvArray("AUTH_AUDIENCES", process.env);
```

## Parity with .NET

Mirrors `DcsvIo.D2.Utilities` extensions:

- `falsey` / `truthy` → `Falsey()` / `Truthy()` extensions on string / collection / Guid
- `toUndefIfEmpty` → `ToNullIfEmpty()` on string (same behavior; TS returns `undefined`, .NET returns `null` — see naming divergence note below)
- `cleanStr` / `cleanDisplayStr` → `CleanStr()` / `CleanDisplayStr()` on string
- `tryParseTruthyUndefUuid` / `tryParseTruthyUndefInt` / `tryParseTruthyUndefEnum` → `TryParseTruthyNull(out ...)` extensions (same behavior; TS returns `undefined`, .NET returns `null`)
- `clean(items, cleaner, opts)` → `IEnumerable<T>.Clean(cleaner, enumEmptyBehavior, valueNullBehavior)` extension; same default behaviors (`ReturnNull` / `RemoveNulls`) and same throw conditions on each side.
- `parseEnvArray` → matches `IConfiguration` array binding (`PREFIX__INDEX` keys)

### Naming divergence from .NET

TS helpers use `*Undef*` naming where the .NET equivalent uses `*Null*`, matching each language's idiomatic "absent" sentinel (TS: `undefined` per rules.md §6.15; .NET: `null` per BCL convention). Behavior is functionally equivalent. See [docs/PARITY.md — Utility helper naming divergence](../../../../docs/PARITY.md#utility-helper-naming-divergence--ts-undef-vs-net-null) for the full table and reviewer guidance.

## Edge cases

- Empty UUID (`"00000000-..."`) is rejected by `tryParseTruthyUndefUuid` — matches .NET `Guid.Empty` collapse.
- `parseEnvArray` stops at the first gap; sparse arrays collapse to dense prefix (matches .NET `IConfiguration`).
- `cleanStr` preserves a single space between words; never returns whitespace-only.
- `tryParseTruthyUndefInt` rejects floats and scientific notation — integer-only.
- `clean` accepts any `Iterable<T>` (arrays, sets, map values, generators); the result is always materialized to an array — generators are exhausted once.

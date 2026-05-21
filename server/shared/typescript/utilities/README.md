<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/utilities

> Parent: [`server/shared/typescript/`](../README.md)

Boundary helpers — `falsey`/`truthy` semantics, string cleaning, parse-or-null
helpers, indexed env-var array parsing, and the regex constants used across the
TS codebase. Mirrors `D2.Shared.Utilities` (.NET).

## Public API

| Export                                                                              | Purpose                                                                                                                   |
| ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| `falsey(value)`                                                                     | True when value is null/undefined/empty/whitespace string/empty collection.                                               |
| `truthy(value)`                                                                     | Inverse of `falsey`.                                                                                                      |
| `toNullIfEmpty(s)`                                                                  | Returns trimmed string or `null` when empty/whitespace.                                                                   |
| `cleanStr(s)`                                                                       | Trims + collapses whitespace runs to one space; null when empty.                                                          |
| `cleanDisplayStr(s)`                                                                | Strips chars not in display-name allowlist + `cleanStr`.                                                                  |
| `tryParseTruthyNullUuid(s)`                                                         | Canonical lowercase UUID on success; null otherwise (empty UUID → null).                                                  |
| `tryParseTruthyNullInt(s)`                                                          | Parsed integer on success; null otherwise.                                                                                |
| `tryParseTruthyNullEnum(enumObj, s)`                                                | Canonical key on case-insensitive match; null otherwise.                                                                  |
| `chunk(arr, size)`                                                                  | Splits into consecutive chunks of `size`; throws on `size < 1`.                                                           |
| `clean(items, cleaner, opts?)`                                                      | Applies a per-element cleaner to any `Iterable<T>`; `opts` chooses null/empty handling — defaults match the .NET sibling. |
| `parseEnvArray(prefix, env)`                                                        | Reads `PREFIX__0=a, PREFIX__1=b, ...` indexed env-var arrays; stops at first gap.                                         |
| `WHITESPACE_RE` / `DISPLAY_NAME_INVALID_RE` / `EMAIL_RE` / `UUID_RE` / `EMPTY_UUID` | Pre-built regex + canonical empty-UUID constants.                                                                         |

## Dependencies

None. Zero runtime deps.

## Usage example

```ts
import { falsey, tryParseTruthyNullUuid, parseEnvArray } from "@d2/utilities";

if (falsey(input)) return null;

const id = tryParseTruthyNullUuid(headers["x-org-id"]);
if (id === null) throw new Error("missing org id");

const audiences = parseEnvArray("AUTH_AUDIENCES", process.env);
```

## Parity with .NET

Mirrors `D2.Shared.Utilities` extensions:

- `falsey` / `truthy` → `Falsey()` / `Truthy()` extensions on string / collection / Guid
- `toNullIfEmpty` → `ToNullIfEmpty()` on string
- `cleanStr` / `cleanDisplayStr` → `CleanStr()` / `CleanDisplayStr()` on string
- `tryParseTruthyNullUuid` / `tryParseTruthyNullInt` / `tryParseTruthyNullEnum` → `TryParseTruthyNull(out ...)` extensions
- `clean(items, cleaner, opts)` → `IEnumerable<T>.Clean(cleaner, enumEmptyBehavior, valueNullBehavior)` extension; same default behaviors (`ReturnNull` / `RemoveNulls`) and same throw conditions on each side.
- `parseEnvArray` → matches `IConfiguration` array binding (`PREFIX__INDEX` keys)

## Edge cases

- Empty UUID (`"00000000-..."`) is rejected by `tryParseTruthyNullUuid` — matches .NET `Guid.Empty` collapse.
- `parseEnvArray` stops at the first gap; sparse arrays collapse to dense prefix (matches .NET `IConfiguration`).
- `cleanStr` preserves a single space between words; never returns whitespace-only.
- `tryParseTruthyNullInt` rejects floats and scientific notation — integer-only.
- `clean` accepts any `Iterable<T>` (arrays, sets, map values, generators); the result is always materialized to an array — generators are exhausted once.

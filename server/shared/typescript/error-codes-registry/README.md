<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/error-codes-registry

Merged cross-catalog error-code registry for D²-WORX. Aggregates every
`*-error-codes.spec.json` catalog under `contracts/` into a single frozen
`code → ErrorCodeInfo` lookup table — so any consuming service can resolve
a wire error code to its full metadata (httpStatus, semantic category,
user-message TK key, factory name/shape, doc, domain) without importing the
producer's catalog.

## Key facts

- **Generated**: `src/generated/error-code-registry.g.ts` is auto-generated
  by `tools/ts-codegen/src/error-codes-registry-emit.ts` — do not hand-edit.
  Re-generate with `pnpm --filter ts-codegen run codegen --force`.
- **Leaf package**: depends only on `@d2/i18n-abstractions` + `@d2/i18n-keys`;
  nothing depends up into it except boundary consumers (Edge/BFF).
- **Collision-safe**: the emitter hard-fails at codegen time on any
  cross-catalog code collision (`D2ERC004`) or reserved-namespace violation
  (`D2ERC005`). No registry is emitted until violations are resolved.
- **Cross-runtime parity**: mirrors `D2.Shared.ErrorCodes.Registry` (.NET)
  — same 8 fields, same domain tokens, same category wire-strings.

## API

```ts
import {
  errorCodeRegistry,
  type ErrorCodeInfo,
} from "@d2/error-codes-registry";

// Resolve a wire code → full metadata (undefined on unknown code).
const info: ErrorCodeInfo | undefined =
  errorCodeRegistry.resolve("AUTH_JWT_EXPIRED");
if (info !== undefined) {
  info.code; // "AUTH_JWT_EXPIRED"
  info.httpStatus; // 401
  info.category; // "validation_failure"
  info.userMessageKey; // TK.auth.errors.UNAUTHORIZED (TKMessage)
  info.factoryName; // "JwtExpired"
  info.factoryShape; // "with_error_code"
  info.doc; // "JWT is expired..."
  info.domain; // "auth"
}

// Membership check.
errorCodeRegistry.has("NOT_FOUND"); // true

// Iterate all registered codes.
for (const entry of errorCodeRegistry.all) {
  /* ... */
}
```

## ErrorCodeInfo fields

| Field            | Type                    | Source                                       |
| ---------------- | ----------------------- | -------------------------------------------- |
| `code`           | `string`                | SCREAMING_SNAKE wire code                    |
| `httpStatus`     | `number`                | HTTP status integer                          |
| `category`       | `ErrorCategory`         | 9-value string-union (schema `category`)     |
| `userMessageKey` | `TKMessage`             | Typed TK constant from `@d2/i18n-keys`       |
| `factoryName`    | `string`                | PascalCase factory symbol                    |
| `factoryShape`   | `ErrorCodeFactoryShape` | 4-value string-union                         |
| `doc`            | `string`                | Developer documentation text                 |
| `domain`         | `string`                | Derived from spec filename (`common`/`auth`) |

## Domain tokens

The `domain` field is derived from the spec filename:

- `contracts/error-codes/error-codes.spec.json` → `"common"`
- `contracts/auth-error-codes/auth-error-codes.spec.json` → `"auth"`
- Future: `contracts/geo-error-codes/geo-error-codes.spec.json` → `"geo"`

## Build

```sh
pnpm --filter @d2/error-codes-registry run build
pnpm --filter @d2/error-codes-registry run test
```

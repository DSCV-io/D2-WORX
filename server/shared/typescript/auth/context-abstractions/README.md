<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/auth-context-abstractions

> Parent: [`server/shared/typescript/`](../../README.md)

`IAuthContext` interface + supporting enums/types. Emitted from
`contracts/auth-context/IAuthContext.spec.json` via `tools/ts-codegen`.
Mirrors `D2.Shared.AuthContext.Abstractions` (.NET).

## Public API

Generated artifacts (committed to git):

| Export                                                 | Source                                                   |
| ------------------------------------------------------ | -------------------------------------------------------- |
| `IAuthContext`                                         | `IAuthContext.g.ts`                                      |
| `IAuthContextRedactPaths`                              | `IAuthContext.g.ts` (PII paths from spec `redact: true`) |
| `OrgType` / `Role` / `ImpersonationKind` / `ActorKind` | `enums/*.g.ts`                                           |
| `ActorEntry`                                           | `types/actor-entry.g.ts`                                 |

## Codegen workflow

`prebuild` runs the `auth-context-emit.ts` script before `tsc -b`, so
`pnpm -r build` regenerates transparently. Force-regen via:

```bash
pnpm --filter ts-codegen codegen --force
```

## Dependencies

None at the package level (interfaces only). The codegen runner
(`tools/ts-codegen`) is a build-time dependency, not a runtime one.

## Usage example

```ts
import type { IAuthContext } from "@d2/auth-context-abstractions";
import {
  OrgType,
  IAuthContextRedactPaths,
} from "@d2/auth-context-abstractions";
import { setupLogger } from "@d2/logging";

const log = setupLogger({
  serviceName: "edge",
  redactPaths: [IAuthContextRedactPaths],
});

function describe(ctx: IAuthContext): string {
  return `${ctx.userId ?? "anon"} on ${ctx.orgId ?? "no-org"} (${
    ctx.orgType ?? OrgType.Customer
  })`;
}
```

## Parity with .NET

Mirrors `D2.Shared.AuthContext.Abstractions`:

- `IAuthContext` — same property set, camelCased per TS conventions.
- `OrgType` / `Role` / `ImpersonationKind` / `ActorKind` — same wire
  values (string-literal unions).
- `ActorEntry` — same field shape.

The .NET-side codegen lives in `D2.Shared.Context.SourceGen` (Roslyn
incremental generator); the TS-side codegen lives in `tools/ts-codegen`.
Both consume the same `IAuthContext.spec.json`.

## Nullability convention

Spec-emitted property types use `T | null` rather than the more idiomatic
TS `T?` / `T | undefined`. This mirrors the .NET side, where
`Nullable<T>` carries an explicit `null` value; serializing the context
envelope across the language boundary preserves the `null` literal so
both sides round-trip the same shape. Optional-chaining and nullish-
coalescing handle either `null` or `undefined` at consumer call sites,
so this convention is opaque to most callers — only matters when you're
constructing or destructuring an `IAuthContext` literal directly.

New domain code outside the codegen-emitted surface should use the
default TS convention (`T?` / `T | undefined`) per the wider TS
codebase's nullability rule. The spec's `?` suffix on a type entry
(e.g. `"string?"` in `IAuthContext.spec.json`) is what triggers the
`| null` emission.

## Edge cases

- `IAuthContextRedactPaths` is empty when no spec entry carries
  `redact: true`. Hand-register additional paths via
  `markRedactedFields()` from `@d2/logging` if needed.
- Generated files (`*.g.ts`) are committed to git so consumers don't
  pay a generate-on-first-build penalty.
- `pnpm exec eslint` ignores generated files — formatting drift is
  irrelevant for spec-derived output.

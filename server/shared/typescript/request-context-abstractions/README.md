<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/request-context-abstractions

> Parent: [`server/shared/typescript/`](../README.md)

`IRequestContext` interface (extends `IAuthContext`) + `IPropagatedContext`
cross-hop subset + `PropagatedContextSerializer` round-trip helper.
Emitted from `contracts/request-context/IRequestContext.spec.json` via
`tools/ts-codegen`. Mirrors `D2.Shared.Context.Abstractions` (.NET).

## Public API

Generated artifacts (committed to git):

| Export                        | Source                                                               |
| ----------------------------- | -------------------------------------------------------------------- |
| `IRequestContext`             | `IRequestContext.g.ts`                                               |
| `IRequestContextRedactPaths`  | `IRequestContext.g.ts` (PII paths from spec `redact: true`)          |
| `IPropagatedContext`          | `IPropagatedContext.g.ts` (cross-hop subset, `propagate: true` only) |
| `PropagatedContextSerializer` | `PropagatedContextSerializer.g.ts` (`serialize` + `tryDecode`)       |

Plus all `@d2/auth-context-abstractions` exports re-exported transitively.

## Codegen workflow

`prebuild` runs the `request-context-emit.ts` script before `tsc -b`, so
`pnpm -r build` regenerates transparently. The emitter reuses the
`emitAuthContext` core for the interface + emits the additional propagated
artifacts.

The emitter honors the spec's `extends` field: when set (as it is for
`IRequestContext.spec.json:6` `"extends": "D2.Shared.AuthContext.Abstractions.IAuthContext"`),
the generated `IRequestContext.g.ts` declares
`export interface IRequestContext extends IAuthContext { ... }` and
emits a `import type { IAuthContext } from "@d2/auth-context-abstractions";`
line at the top. Consumers see the unified `IRequestContext` shape as a
single import — IAuthContext properties are inherited transitively.

## Dependencies

- `@d2/auth-context-abstractions` — `IAuthContext` + supporting enums.

## Usage example

```ts
import {
  PropagatedContextSerializer,
  IRequestContextRedactPaths,
} from "@d2/request-context-abstractions";
import { setupLogger } from "@d2/logging";

// Encode envelope for cross-hop propagation.
const envelope = PropagatedContextSerializer.serialize(propagatedCtx);
amqpHeaders["x-d2-context"] = envelope;

// Decode incoming envelope (returns undefined on tamper / oversize).
const decoded = PropagatedContextSerializer.tryDecode(headers["x-d2-context"]);

// Wire PII paths into Pino redaction.
const log = setupLogger({
  serviceName: "edge",
  redactPaths: [IRequestContextRedactPaths],
});
```

## Parity with .NET

Mirrors `D2.Shared.Context.Abstractions`:

- `IRequestContext` ↔ same property set, camelCased.
- `PropagatedContextSerializer` ↔ 1:1 type-named class with
  `Serialize`/`Deserialize`.
- `IPropagatedContext` ↔ same propagated subset.

## Nullability convention

> See [`../auth-context-abstractions/README.md` § Nullability convention](../auth-context-abstractions/README.md#nullability-convention) for the canonical rule. `IRequestContext` extends `IAuthContext` and inherits the same spec-driven `T | null` emission for spec-emitted property types (the spec's `?` suffix on a type entry — e.g. `"string?"` in `IRequestContext.spec.json` — triggers the `| null` emission).

## Edge cases

- `tryDecode` enforces per-field `maxLength` caps from the spec — a
  forged envelope with any cap exceeded is dropped wholesale (returns
  `undefined`). Propagation is opportunistic, never required.
- `tryDecode` type-checks numeric / boolean fields and rejects on
  shape mismatch.
- `null` values survive serialize → deserialize without becoming the
  string `"null"`.
- Generated files (`*.g.ts`) are committed to git.

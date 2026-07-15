<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-i18n-abstractions

> Parent: [`public/packages/typescript/`](../README.md)

Foundational, zero-dependency package that declares the i18n primitive types:
the `TKMessage` translation-message shape, the `tk()` factory that constructs
one, and the spec-derived `TkMessageWireShape` property-name catalog. Mirrors
`DcsvIo.D2.I18n.Abstractions` on the .NET side — a leaf package with no outbound
dependencies, so any package in the graph can import these primitives without
risking a circular dependency.

## Public API

| Export               | Purpose                                                                                                                                           |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TKMessage`          | The translation-message shape — `{ key: string; params?: Record<string, unknown> }`. Carried by `D2Result.messages` and rendered by a translator. |
| `tk`                 | Factory constructing a `TKMessage` from a key plus optional params. Mirrors the .NET `new TKMessage(key, params)` ergonomics.                     |
| `TkMessageWireShape` | Spec-derived JSON property-name catalog (`KEY`, `PARAMS`) for the `TKMessage` wire shape — referenced by serializers instead of inline literals.  |

`TkMessageWireShape` is auto-generated from `contracts/tk-message/tk-message.spec.json`
by `tools/ts-codegen`. Do not edit `src/generated/tk-message.g.ts` by hand —
changes will be overwritten on the next codegen run.

## Dependencies

None. Zero runtime deps — this is a foundational leaf in the dependency graph.

## Usage example

```ts
import { tk, type TKMessage } from "@dcsv-io/d2-i18n-abstractions";

const message: TKMessage = tk("common_errors_NOT_FOUND");
// message === { key: "common_errors_NOT_FOUND" }

const withParams = tk("common_errors_LIMIT_EXCEEDED", { maxLength: 256 });
// withParams === { key: "common_errors_LIMIT_EXCEEDED", params: { maxLength: 256 } }
```

## Why a separate package

`TKMessage` is the shared currency of the result + i18n surfaces: `@dcsv-io/d2-result`
carries `TKMessage[]` on every `D2Result`, `@dcsv-io/d2-i18n-keys` exposes the `TK`
constants as `TKMessage` instances, and `@dcsv-io/d2-i18n` renders them. Placing the
type and its factory in a zero-dependency leaf lets all three depend on it
without forming a cycle, and keeps `@dcsv-io/d2-result` focused on the result envelope
rather than owning an i18n primitive.

## Parity with .NET

Mirrors `DcsvIo.D2.I18n.Abstractions` — both declare `TKMessage` plus the
spec-derived wire-shape property names, generated from the same
`contracts/tk-message/tk-message.spec.json` source. Single spec, two emitters,
cross-language wire drift structurally impossible.

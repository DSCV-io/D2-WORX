<!--
Copyright (c) DCSV. All rights reserved.
-->

# @dcsv-io/d2-i18n-keys

> Parent: [`public/packages/typescript/`](../README.md)

Foundational package that exports the type-safe TK constants catalog. Mirrors
`DcsvIo.D2.I18n.Keys` on the .NET side — a thin keys layer whose only dependency
is the i18n-abstractions package (for the `TKMessage` type + `tk()` factory), so
any package in the graph can import TK constants without risking a circular
dependency.

## Public API

| Export  | Purpose                                                                                                                                                        |
| ------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TK`    | Nested object of `TKMessage` instances, each whose `.key` is its snake-case message key (e.g. `TK.common.errors.NOT_FOUND.key === "common_errors_NOT_FOUND"`). |
| `TKKey` | `string` type alias for a raw TK key value.                                                                                                                    |

Both are auto-generated from `contracts/messages/en-US.json` by
`tools/ts-codegen`. Do not edit `src/generated/tk-keys.g.ts` by hand — changes
will be overwritten on the next codegen run.

## Dependencies

`@dcsv-io/d2-i18n-abstractions` (the `TKMessage` type + `tk()` factory the generated
constants are built from). That package is a zero-dependency leaf, so this
package stays shallow in the graph.

## Usage example

```ts
import { TK } from "@dcsv-io/d2-i18n-keys";

// TK.common.errors.NOT_FOUND === { key: "common_errors_NOT_FOUND" }
// Drop straight into D2Result.messages — no tk() wrapper needed:
//   notFound({ messages: [TK.common.errors.NOT_FOUND] })
console.log(TK.common.errors.NOT_FOUND.key);
```

## Why a separate package

The TK constants are needed by both `@dcsv-io/d2-result` (for factory default messages)
and `@dcsv-io/d2-i18n` (the Translator and Paraglide runtime). If the constants lived
in `@dcsv-io/d2-i18n`, packages that depend on `@dcsv-io/d2-result` could not also transitively
import the constants without creating a cycle (`result → i18n → result`).
Keeping the constants in this shallow package — whose only dependency is the
zero-dep `@dcsv-io/d2-i18n-abstractions` leaf — breaks the cycle structurally: any
package may import `@dcsv-io/d2-i18n-keys` regardless of where it sits in the dependency
graph.

Consumers of the `@dcsv-io/d2-i18n/keys` re-export (`import { TK } from "@dcsv-io/d2-i18n/keys"`)
continue to work unchanged — `@dcsv-io/d2-i18n` re-exports from this package for
backward compatibility.

## Parity with .NET

Mirrors `DcsvIo.D2.I18n.Keys` (`TK.g.cs`). Both are generated from the same
`contracts/messages/en-US.json` source and expose the TK constants as `TKMessage`
instances built on the abstractions package — single spec, two emitters, drift
structurally impossible.

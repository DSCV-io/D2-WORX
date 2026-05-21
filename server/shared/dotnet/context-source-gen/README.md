<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Context.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits context interfaces + the
mutable concrete class from JSON spec files. Multi-target — dispatches per
consuming assembly.

The spec files are the single source of truth for the auth + request context
shape. Adding a property is a one-line change to
`contracts/auth-context/IAuthContext.spec.json` or
`contracts/request-context/IRequestContext.spec.json` — the interface, the
mutable concrete, and the two factory methods (`FromClaims`,
`FromJwtPayloadNoValidation`) all update on next build.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

> **Cross-hop propagation does NOT go through codegen.** The small subset of
> fields a downstream consumer can't recompute (`RequestId`, `RequestPath`,
> fingerprints, `WhoIsHashId`) is propagated via the hand-written
> `PropagatedContext` record + serializer in `D2.Shared.Context.Abstractions`,
> wired into transport headers (`x-d2-context` on AMQP / gRPC / HTTP).
> Identity (UserId, OrgId, Scopes, etc.) rebuilds at every hop from the JWT
> — never propagated.

---

## Catalog dispatch

| Assembly | Emitted file(s) |
|---|---|
| `D2.Shared.AuthContext.Abstractions` | `IAuthContext.g.cs` |
| `D2.Shared.Context.Abstractions` | `IRequestContext.g.cs` (extends `IAuthContext`) |
| `D2.Shared.Context.Abstractions` | `MutableRequestContext.g.cs` |
| `D2.Shared.Context.Abstractions` | `PropagatedContext.g.cs` (cross-hop wire record) |
| `D2.Shared.Context.Abstractions` | `PropagatedContextExtensions.g.cs` (`MutableRequestContext.ToPropagated()` / `Apply()`) |
| `D2.Shared.Context.Abstractions` | `PropagatedContextSerializer.g.cs` (`Encode` / `TryDecode` for transport headers) |
| Anything else | nothing |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2CTX001` | Error | Spec file is malformed JSON or violates the schema |
| `D2CTX002` | Error | Spec property uses a type outside the closed vocabulary |
| `D2CTX003` | Error | Two properties in the combined spec hierarchy declare the same name |
| `D2CTX004` | Error | `extends` references an interface not surfaced to the generator |
| `D2CTX005` | Warning | Property declared `derived` with an unrecognized rule name |
| `D2CTX006` | Error | No `*.spec.json` found in `AdditionalFiles` for a target assembly |

---

## Spec-driven `[RedactData]` placement

Setting `"redact": true` on a property in either spec marks it as PII-bearing.
The generator emits `[RedactData(Reason = RedactReason.PersonalInformation)]`
on BOTH the corresponding interface property AND the matching concrete
property on `MutableRequestContext`. The Serilog destructuring policy
(`D2.Shared.Logging.Destructuring.RedactDataDestructuringPolicy`) reflects
on the runtime instance type at log time, so the attribute on the concrete
is what makes redaction fire; the attribute on the interface is what
keeps the cross-spec parity gate
(`server/shared/dotnet/tests/Unit/SpecsConsistency/RedactDataVsSpecRedactConsistencyTests.cs`)
honest. The two MUST stay in lockstep — codegen places both unconditionally
when the spec says `redact: true`, so divergence requires hand-editing
the generated output (which is fenced by the auto-generated banner).

The TS-side codegen (`tools/ts-codegen/src/{auth-context,request-context}-emit.ts`)
emits a sibling `<TypeName>RedactPaths` constant from the same spec field,
fed into `setupLogger({ redactPaths })` for Pino's redact configuration.
The spec is the single source of truth across both languages.

## Closed type vocabulary

These are the only types a spec property can declare (enforced via `D2CTX002`):

```
string?, bool?, int?, double?, Guid?, DateTimeOffset?,
OrgType?, Role?, ActorKind?, ImpersonationKind?,
IReadOnlyList<ActorEntry>, IReadOnlyList<string>, IReadOnlySet<string>
```

New types require schema (`*.spec.json`'s JSON Schema enum) + `TypeVocabulary` + `MutableEmitter` per-type emit helpers — all in lockstep.

## Derived rules

Currently only one derived rule is recognized:

| Rule | Effect |
|---|---|
| `actorChain` | Emitted as a computed getter that walks `ActorChain` to compute impersonation flavor / impersonator org / service-client-id / etc. |

Adding a rule requires extending `TypeVocabulary.IsValidDerivedRule` plus a per-property emit case in `MutableEmitter.EmitActorChainDerivedGetter` (or a new helper for the new rule).

---

## Generated runtime helpers (provided by the request-context concrete lib, NOT this generator)

The generated `MutableRequestContext.g.cs` references:

- `ActorChainParser.ParseFromJson(JsonElement)` / `ActorChainParser.ParseFromJsonString(string)` — RFC 8693 actor-chain parsing
- `ScopeClaimParser.Parse(JsonElement)` / `ScopeClaimParser.ParseString(string)` — RFC 6749 §3.3 space-separated string OR JSON-array scope parsing

These hand-written helpers live in `D2.Shared.Context.Abstractions` — the parsing rules are stable RFC text and don't benefit from spec-driven codegen. Tests for the parsers pin RFC compliance.

---

## Reference

- [`docs/SRC_GEN.md`](../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`contracts/auth-context/`](../../../../contracts/auth-context/) — auth-context spec + JSON Schema
- [`contracts/request-context/`](../../../../contracts/request-context/) — request-context spec + JSON Schema
- [`D2.Shared.Auth.Scopes.SourceGen`](../auth-scopes-source-gen/) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [RFC 8693 §2.1](https://datatracker.ietf.org/doc/html/rfc8693#section-2.1) — actor chain semantics
- [RFC 6749 §3.3](https://datatracker.ietf.org/doc/html/rfc6749#section-3.3) — `scope` claim format

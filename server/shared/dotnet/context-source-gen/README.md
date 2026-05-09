<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Context.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits context interfaces + the
mutable concrete class from JSON spec files. Multi-target — dispatches per
consuming assembly:

| Assembly | Emitted file(s) |
|---|---|
| `D2.Shared.AuthContext.Abstractions` | `IAuthContext.g.cs` |
| `D2.Shared.Context.Abstractions` | `IRequestContext.g.cs` (extends `IAuthContext`) |
| `D2.Shared.Context.Abstractions` | `MutableRequestContext.g.cs` |
| Anything else | nothing |

The spec files are the single source of truth for the auth + request context
shape. Adding a property is a one-line change to
`contracts/auth-context/IAuthContext.spec.json` or
`contracts/request-context/IRequestContext.spec.json` — the interface, the
mutable concrete, and the two factory methods (`FromClaims`,
`FromJwtPayloadNoValidation`) all update on next build.

> **Cross-hop propagation does NOT go through codegen.** The small subset of
> fields a downstream consumer can't recompute (`RequestId`, `RequestPath`,
> fingerprints, `WhoIsHashId`) is propagated via the hand-written
> `PropagatedContext` record + serializer in `D2.Shared.Context.Abstractions`,
> wired into transport headers (`x-d2-context` on AMQP / gRPC / HTTP).
> Identity (UserId, OrgId, Scopes, etc.) rebuilds at every hop from the JWT
> — never propagated.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.Context.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | `init`-accessor polyfill for `netstandard2.0` records |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` — value-equatable for incremental cache stability |
| `ContextSpec.cs` / `Section.cs` / `PropertySpec.cs` | Parsed-shape records |
| `TypeVocabulary.cs` | Closed type vocab + derived-rule vocab + per-type default-value map |
| `SpecLoader.cs` | JSON → `ContextSpec` parser. Emits `D2CTX001` on parse failure |
| `LoadResult.cs` | `(Spec?, Diagnostic?)` |
| `InterfaceEmitter.cs` | `ContextSpec` → interface `*.g.cs` (single-spec). Validates types + derived rules |
| `MutableEmitter.cs` | `(authSpec, requestSpec)` → `MutableRequestContext.g.cs`. Cross-spec property collision check |
| `EmitDiagnostic.cs` / `EmitResult.cs` | Roslyn-decoupled diagnostic + emit-result records |
| `DiagnosticIds.cs` / `DiagnosticDescriptors.cs` | `D2CTX001`–`D2CTX006` |
| `ContextGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `*.spec.json`, dispatches per assembly |

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

## Closed type vocabulary

These are the only types a spec property can declare (enforced via `D2CTX002`):

```
string?, bool?, int?, double?, Guid?, DateTimeOffset?,
OrgType?, Role?, ActorKind?, ImpersonationKind?,
IReadOnlyList<ActorEntry>, IReadOnlySet<string>
```

New types require schema (`*.spec.json`'s JSON Schema enum) + `TypeVocabulary` + `MutableEmitter` per-type emit helpers — all in lockstep.

## Derived rules

Currently only one derived rule is recognized:

| Rule | Effect |
|---|---|
| `actorChain` | Emitted as a computed getter that walks `ActorChain` to compute impersonation flavor / impersonator org / service-client-id / etc. |

Adding a rule requires extending `TypeVocabulary.IsValidDerivedRule` plus a per-property emit case in `MutableEmitter.EmitActorChainDerivedGetter` (or a new helper for the new rule).

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.AuthContext.Abstractions</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\auth-abstractions\D2.Shared.Auth.Abstractions.csproj" />
    <ProjectReference Include="..\context-source-gen\D2.Shared.Context.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\auth-context\IAuthContext.spec.json" />
    <AdditionalFiles Include="..\..\..\..\contracts\request-context\IRequestContext.spec.json" />
  </ItemGroup>
</Project>
```

The per-assembly dispatch in `ContextGenerator` ensures only assemblies named in the dispatch table get emission. Other consumers (test projects, runtime libs that just reference `auth-context-abstractions`) get the analyzer DLL but no generated source.

---

## Generated runtime helpers (provided by the request-context concrete lib, NOT this generator)

The generated `MutableRequestContext.g.cs` references:

- `ActorChainParser.ParseFromJson(JsonElement)` / `ActorChainParser.ParseFromJsonString(string)` — RFC 8693 actor-chain parsing
- `ScopeClaimParser.Parse(JsonElement)` / `ScopeClaimParser.ParseString(string)` — RFC 6749 §3.3 space-separated string OR JSON-array scope parsing

These hand-written helpers live in `D2.Shared.Context.Abstractions` — the parsing rules are stable RFC text and don't benefit from spec-driven codegen. Tests for the parsers pin RFC compliance.

---

## Reference

- [`contracts/auth-context/`](../../../../contracts/auth-context/) — auth-context spec + JSON Schema
- [`contracts/request-context/`](../../../../contracts/request-context/) — request-context spec + JSON Schema
- [`D2.Shared.Auth.Scopes.SourceGen`](../auth-scopes-source-gen/) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [RFC 8693 §2.1](https://datatracker.ietf.org/doc/html/rfc8693#section-2.1) — actor chain semantics
- [RFC 6749 §3.3](https://datatracker.ietf.org/doc/html/rfc6749#section-3.3) — `scope` claim format

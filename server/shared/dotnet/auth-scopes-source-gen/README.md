<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Auth.Scopes.SourceGen

> Parent: [`server/shared/dotnet/`](../README.md)

Roslyn incremental source generator that emits the `Scopes` static partial class into `D2.Shared.Auth.Abstractions` by reading `contracts/auth-scopes/scopes.spec.json` via `<AdditionalFiles>`. Single-target — emits ONLY when the consuming assembly is `D2.Shared.Auth.Abstractions`.

The spec file is the single source of truth for the platform's scope catalog. Every scope a handler can require, every scope Edge mints into a token, and every grant-matrix entry lives in one JSON file — no hand-written parallel constants, no per-feature drift.

---

## File layout

| Path | Role |
|---|---|
| `D2.Shared.Auth.Scopes.SourceGen.csproj` | csproj — `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"` on Roslyn deps + bundled `System.Text.Json` |
| `Polyfills/IsExternalInit.cs` | Polyfill enabling `init` accessors on `netstandard2.0` records |
| `SpecFile.cs` | Pipeline-boundary record `(Path, Content)` — value-equatable for incremental cache stability |
| `ScopesSpec.cs` | Parsed-shape record for the top-level spec — `(IReadOnlyList<ScopeEntry> Scopes)` |
| `ScopeEntry.cs` | Parsed-shape record for one spec entry — `(string Name, string Description, string ActionSensitivity, bool ImpersonationBlocked, IReadOnlyDictionary<string, string[]>? GrantedTo)` |
| `ScopeSpecLoader.cs` | JSON → `ScopesSpec` parser. Emits `D2SCP001` on parse failure |
| `ScopesEmitter.cs` | `ScopesSpec` → C# source. Validates names, enum values, tree positions, etc. Emits `D2SCP002`–`D2SCP008` |
| `EmitDiagnostic.cs` | Roslyn-decoupled diagnostic record + factories |
| `EmitResult.cs` | `(GeneratedSource, ImmutableArray<EmitDiagnostic>)` |
| `DiagnosticIds.cs` | String IDs `D2SCP001`–`D2SCP009` (Roslyn-decoupled — pure-logic tests can reference) |
| `DiagnosticDescriptors.cs` | Roslyn `DiagnosticDescriptor` instances (loaded only inside the host) |
| `ScopesGenerator.cs` | `[Generator]` `IIncrementalGenerator`. Filters AdditionalFiles to `scopes.spec.json`, gates by assembly name, drives the loader + emitter |

---

## Build-time diagnostics

| ID | Severity | Trigger |
|---|---|---|
| `D2SCP001` | Error | Spec file is malformed JSON or violates the schema |
| `D2SCP002` | Error | `grantedTo` references unknown `OrgType` / `Role` enum value |
| `D2SCP003` | Error | Scope name violates naming convention (lowercase dot-separated; segments must be valid C# identifiers; ≥ 2 segments; no consecutive / leading / trailing dots) |
| `D2SCP004` | Error | Duplicate scope name |
| `D2SCP005` | Warning | Anonymous scope marked `impersonationBlocked` (meaningless — anon scopes are pre-auth) |
| `D2SCP006` | Error | `grantedTo` entry has empty role array — invalid config (omit the entry instead) |
| `D2SCP007` | Error | Two scopes collide at the same tree position (one is a strict dot-prefix of the other) |
| `D2SCP008` | Error | Non-anonymous scope omits `grantedTo` (unreachable scope) |
| `D2SCP009` | Error | No `scopes.spec.json` found in `AdditionalFiles` |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "scopes": [
    {
      "name": "anon.public.health",
      "description": "Public health-check endpoint.",
      "actionSensitivity": "Routine",
      "impersonationBlocked": false
    },
    {
      "name": "auth.user.impersonate.consent",
      "description": "Initiate consent-based impersonation.",
      "actionSensitivity": "Sensitive",
      "impersonationBlocked": true,
      "grantedTo": { "Support": ["Officer"], "Admin": ["Owner", "Officer"] }
    },
    {
      "name": "billing.payment.charge",
      "description": "Initiate a payment charge.",
      "actionSensitivity": "Critical",
      "impersonationBlocked": true,
      "grantedTo": { "Customer": ["Owner"] }
    }
  ]
}
```

### Field rules

- **`name`**: dot-separated, lowercase, ≥ 2 segments, each segment `^[a-z][a-z0-9]*$`. Anonymous scopes start with `anon.`.
- **`description`**: free-form. Renders as XML `<summary>` on the emitted constant.
- **`actionSensitivity`**: one of `Routine` / `Sensitive` / `Critical`. Drives audit verbosity, OTP step-up, impersonation defaults.
- **`impersonationBlocked`**: `true` → Edge strips this scope from impersonated tokens at mint time (defense in depth — `RequiredScopes` check still rejects naturally). Meaningless on anon scopes (`D2SCP005`).
- **`grantedTo`**: per-(`OrgType`, `Role`) grant matrix. Keys: `OrgType` name (PascalCase) or `"*"`. Values: array of `Role` names (PascalCase) or `["*"]`. **Empty role arrays are forbidden** (`D2SCP006`) — for "no grant," omit the entry. Required for non-anon scopes (`D2SCP008`); omitted on anon (universal pre-auth grant by namespace convention).

### Wildcard expansion

`*` for org type expands against `D2.Shared.Auth.Abstractions.OrgType` enum members; `*` for role expands against `D2.Shared.Auth.Abstractions.Role` members. Expansion happens at **codegen time**, not runtime — adding a new enum member requires re-running the build to pick it up (the emitted `Scopes.g.cs` carries an `// auto-generated` header listing the enum members it expanded against).

---

## Emitted `Scopes.g.cs` shape

```csharp
public static partial class Scopes
{
    public static class Anon
    {
        public static class Public
        {
            public const string Health = "anon.public.health";
        }
        // ...
    }

    public static class Auth
    {
        public static class User
        {
            public static class Impersonate
            {
                public const string Consent = "auth.user.impersonate.consent";
                public const string Force = "auth.user.impersonate.force";
            }
        }
        // ...
    }

    public static ActionSensitivity GetActionSensitivity(string scope);
    public static bool IsImpersonationBlocked(string scope);
    public static bool IsAnonymous(string scope);
    public static bool IsKnown(string scope);
    public static bool IsGrantedTo(string scope, OrgType orgType, Role role);

    public static IReadOnlySet<string> AllScopes;
    public static IReadOnlySet<string> AllAnonymousScopes;
    public static IReadOnlySet<string> AllImpersonationBlockedScopes;
    public static IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> GrantedScopes;
}
```

All lookup helpers are O(1) — backed by `HashSet<string>` / `Dictionary<,>`. `GrantedScopes` is the canonical `(OrgType, Role) → scope-set` map Edge consumes at JWT mint time.

---

## Wiring into a consuming csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.Auth.Abstractions</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\auth-scopes-source-gen\D2.Shared.Auth.Scopes.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\auth-scopes\scopes.spec.json" />
  </ItemGroup>
</Project>
```

The single-target dispatch in `ScopesGenerator` ensures only assemblies named `D2.Shared.Auth.Abstractions` get the emitted `Scopes.g.cs` — other consumers (test projects, runtime libs that just reference `auth-abstractions`) get the analyzer DLL but no emission.

---

## Reference

- [`contracts/auth-scopes/schema.json`](../../../../contracts/auth-scopes/schema.json) — JSON Schema for the spec
- [`contracts/auth-scopes/scopes.spec.json`](../../../../contracts/auth-scopes/scopes.spec.json) — the source-of-truth scope catalog
- [`D2.Shared.I18n.SourceGen`](../i18n-source-gen/) — sibling SrcGen this one mirrors (same incremental-generator + diagnostic-split pattern)
- [`docs/RATE-LIMITING.md`](../../../../docs/RATE-LIMITING.md) — companion `RateLimitTier` enum (lives in Edge — orthogonal axis)

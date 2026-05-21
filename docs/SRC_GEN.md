<!--
Copyright (c) DCSV. All rights reserved.
-->

# SRC_GEN.md — Spec-driven codegen across .NET + TypeScript

Canonical reference for D²-WORX's spec-driven codegen pattern — module authors
adding new shared catalogs, codegen-emitter authors, and consumers of generated
constants find here the single source for how JSON specs become typed code in
both .NET (Roslyn `IIncrementalGenerator`) and TypeScript (`tools/ts-codegen`).

---

## Table of contents

- [§0. Why spec-driven codegen](#0-why-spec-driven-codegen)
- [§1. .NET — Roslyn IIncrementalGenerator](#1-net--roslyn-iincrementalgenerator)
  - [§1.1. JSON specs as `<AdditionalFiles>`](#11-json-specs-as-additionalfiles)
  - [§1.2. Diagnostic ID convention](#12-diagnostic-id-convention)
  - [§1.3. Generator anatomy (incremental + filter + emit)](#13-generator-anatomy-incremental--filter--emit)
  - [§1.4. Example walkthrough — i18n](#14-example-walkthrough--i18n)
- [§2. TypeScript — tools/ts-codegen](#2-typescript--toolstscodegen)
  - [§2.1. JSON specs as inputs](#21-json-specs-as-inputs)
  - [§2.2. Emitter pattern](#22-emitter-pattern)
  - [§2.3. Output shape](#23-output-shape)
  - [§2.4. Example walkthrough — auth-error-codes](#24-example-walkthrough--auth-error-codes)
- [§3. Adding a new spec-driven catalog](#3-adding-a-new-spec-driven-catalog)
- [References](#references)

---

## §0. Why spec-driven codegen

D²-WORX uses spec-driven codegen for every cross-language constant catalog —
error codes, scopes, audiences, JWT claim names, wire-format headers, OTel
attribute names, encryption-frame byte offsets, RFC 7807 ProblemDetails keys,
the messaging registry, and more. The pattern's load-bearing properties:

- **Single source of truth.** A JSON spec under `contracts/<topic>/<topic>.spec.json`
  is the canonical declaration; both .NET and TypeScript consumers derive their
  typed constants from the same file. Drift between the two language sides is
  structurally impossible.
- **Compile-time enforcement.** Renaming a constant breaks every consumer at
  compile time (.NET) or at build (TypeScript). The bug class "string literal
  drifted out of sync with the wire value" cannot ship.
- **Codegen output is committed to git.** The emitted `.g.cs` / `.g.ts` files
  are checked in (with `linguist-generated=true` so they show condensed in
  diffs) so PR review can inspect them, IDEs can navigate to them, and CI
  doesn't have to re-emit before compiling.
- **No template engines, no AST manipulation.** The emitters use plain string
  builders — `StringBuilder.AppendLine` on .NET, the `string-builder.ts` helper
  on TypeScript. Mirrored shape on both sides so a single engineer can fluently
  switch between the two.
- **Hand-mirrored constants are forbidden.** When a spec catalog exists for a
  concept, hand-writing a parallel constant in the consumer code is a process
  violation — the codegen is the only blessed surface for that catalog.

The two halves of the system mirror each other:

- **.NET side**: Roslyn `IIncrementalGenerator` per topic, csproj-named
  `*-source-gen/`, lives under `server/shared/dotnet/`. Emits per-target-assembly
  via single-target dispatch keyed on the consuming assembly name.
- **TypeScript side**: per-topic `tsx` scripts under `tools/ts-codegen/src/`,
  invoked via `pnpm codegen`. Emits one `.g.ts` per consuming package.

Both halves consume the same JSON specs under `contracts/<topic>/`. The
emitters re-use the same diagnostic IDs (`D2I18N*`, `D2SCP*`, `D2AEC*`, etc.)
because a malformed spec is malformed regardless of which language is reading
it.

---

## §1. .NET — Roslyn IIncrementalGenerator

### §1.1. JSON specs as `<AdditionalFiles>`

Each consuming csproj wires the spec file in via the `<AdditionalFiles>`
mechanism:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>D2.Shared.I18n.Abstractions</RootNamespace>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)Generated</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\i18n-source-gen\D2.Shared.I18n.SourceGen.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <AdditionalFiles Include="..\..\..\..\contracts\messages\*.json" />
  </ItemGroup>
</Project>
```

Three load-bearing properties of this wiring:

- `OutputItemType="Analyzer"` + `ReferenceOutputAssembly="false"` — the
  source-gen dll runs at compile time but is NEVER added to the consumer's
  runtime closure. Zero runtime cost.
- `EmitCompilerGeneratedFiles=true` + `CompilerGeneratedFilesOutputPath` —
  the emitted `.g.cs` files land in `obj/$(Configuration)/$(TargetFramework)/Generated/`
  so PR review + IDE navigation + git tracking work seamlessly.
- `<AdditionalFiles>` paths can be glob patterns (`*.json`) when a spec topic
  has multiple per-locale or per-domain files (e.g. i18n) — the generator
  filters within itself to the files it cares about.

### §1.2. Diagnostic ID convention

Each source-gen owns a `D2<TOPIC>NNN` diagnostic ID prefix (3-5 char topic
abbreviation, 3-digit number). Examples currently in use:

| Topic                | Prefix    | Owning source-gen                |
| -------------------- | --------- | -------------------------------- |
| i18n                 | `D2I18N`  | `i18n-source-gen`                |
| Auth scopes          | `D2SCP`   | `auth-scopes-source-gen`         |
| Auth audiences       | `D2AUD`   | `auth-audiences-source-gen`      |
| Auth error codes     | `D2AEC`   | `auth-error-codes-source-gen`    |
| Generic error codes  | `D2EC`    | `error-codes-source-gen`         |
| D2Result envelope    | `D2RES`   | `d2result-envelope-source-gen`   |
| Headers              | `D2HDR`   | `headers-source-gen`             |
| JWT claims           | `D2JWT`   | `jwt-claims-source-gen`          |
| Messaging registry   | `D2MQ`    | `messaging-source-gen`           |
| DLQ failure metadata | `D2DLQ`   | `dlq-failure-metadata-source-gen`|
| OTel messaging tags  | `D2OTM`   | `otel-messaging-tags-source-gen` |
| Telemetry tags       | `D2TT`    | `telemetry-tags-source-gen`      |
| Encryption domains   | `D2ENCD`  | `encryption-domains-source-gen`  |
| Encryption frame     | `D2ENCF`  | `encryption-frame-source-gen`    |
| ProblemDetails       | `D2PD`    | `problem-details-source-gen`     |
| Wire shapes          | `D2WS`    | `wire-shapes-source-gen`         |
| Context              | `D2CTX`   | `context-source-gen`             |
| gRPC trailers        | `D2GT`    | `grpc-trailers-source-gen`       |
| In-process keys      | `D2IPK`   | `in-process-keys-source-gen`     |

Diagnostic IDs are declared in two parallel files:

- `DiagnosticIds.cs` — pure string constants. Roslyn-decoupled so pure-logic
  emitter tests can reference them without taking a `Microsoft.CodeAnalysis`
  dep.
- `DiagnosticDescriptors.cs` — Roslyn `DiagnosticDescriptor` instances loaded
  only inside the host generator.

The split lets emitter tests verify "this spec produces diagnostic D2MQ004"
without spinning up a full Roslyn compilation; the generator's `Execute`
method passes the actual `DiagnosticDescriptor` to `context.ReportDiagnostic`.

### §1.3. Generator anatomy (incremental + filter + emit)

Every D²-WORX .NET source-gen follows the same shape:

```csharp
[Generator]
public sealed class TKGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Pipeline: AdditionalFiles → SpecFile records (value-equatable)
        var localeFiles = context.AdditionalTextsProvider
            .Where(at => at.Path.EndsWith(".json", StringComparison.Ordinal))
            .Where(at => at.Path.Contains("contracts/messages/", StringComparison.Ordinal))
            .Select((at, ct) => new LocaleFile(
                Path: at.Path,
                LocaleCode: Path.GetFileNameWithoutExtension(at.Path),
                Content: at.GetText(ct)?.ToString() ?? string.Empty));

        // 2. Aggregate the per-locale stream + the consuming assembly name
        var combined = localeFiles
            .Collect()
            .Combine(context.CompilationProvider.Select((c, _) => c.AssemblyName));

        // 3. Single-target dispatch — gate by consuming-assembly name
        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var (files, assemblyName) = tuple;
            if (assemblyName != "D2.Shared.I18n.Abstractions") return;

            // 4. Load + emit
            var emitResult = TKEmitter.Emit(files);
            foreach (var diag in emitResult.Diagnostics) spc.ReportDiagnostic(diag.ToRoslyn());
            if (emitResult.GeneratedSource is { } source) spc.AddSource("TK.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }
}
```

Five load-bearing decisions:

1. **Incremental pipeline boundaries are value-equatable records.** Wrapping
   `AdditionalText` content in a `SpecFile(Path, Content)` record (with
   value-equality) is what enables Roslyn's incremental cache — unchanged spec
   files don't re-trigger the emit.
2. **Filter early.** The `.Where(at => at.Path.EndsWith(".json"))` filter runs
   at the IncrementalValuesProvider level — only files that pass the filter
   reach the heavier `.Select` / `.Combine` stages.
3. **Single-target dispatch by consuming-assembly name.** A source-gen may be
   referenced as Analyzer by multiple consumers, but its emission gates on the
   single assembly that owns the catalog. Other consumers get the analyzer dll
   but no emitted source. Multi-target dispatch (e.g. `dlq-failure-metadata-source-gen`
   emitting different classes into messaging-abstractions AND messaging-rabbitmq)
   uses one source-gen with two assembly-name branches.
4. **Loader / Emitter / Generator separation.** The Generator is the Roslyn
   host wrapper; the Loader (`<X>Loader.cs`) parses JSON → typed record; the
   Emitter (`<X>Emitter.cs`) takes the typed record → emit-result (string +
   diagnostics). The Loader + Emitter are pure-logic and live in
   `xUnit`-testable code without Roslyn dependencies.
5. **Roslyn-decoupled diagnostic records.** `EmitDiagnostic(Id, Severity,
   Message, Location?)` carries everything the host needs but doesn't take a
   Roslyn type dep. The host wrapper `.ToRoslyn()` extension does the
   conversion at the edge.

### §1.4. Example walkthrough — i18n

`i18n-source-gen` is the canonical pattern this codebase reaches for. It emits
the `TK` static class — a hierarchical catalog of `TKMessage` constants — into
`D2.Shared.I18n.Abstractions` by reading `contracts/messages/*.json`
translation catalogs.

**1. Author the spec.**

```json
// contracts/messages/en-US.json
{
  "common_errors_NOT_FOUND": "The requested resource was not found.",
  "common_errors_UNAUTHORIZED": "You are not authorized.",
  "auth_errors_INVALID_ROLE": "The role '{role}' is not valid for org '{org}'."
}
```

Key shape: `<domain>_<category>_<NAME>`. Values: free-form translation
strings; embedded `{name}` placeholders correspond to `TKMessage.With(name,
value)` parameter substitution at render time.

One JSON file per locale (`en-US.json`, `fr-FR.json`, etc.). en-US is the
source of truth for the catalog; other locales contribute coverage diagnostics.

**2. KeyDecomposer validates each key.**

The pure-logic `KeyDecomposer.Decompose(rawKey)` enforces:

- ≥ 2 underscore-separated segments.
- Each segment is a valid C# identifier (`^[A-Za-z_][A-Za-z0-9_]*$`).
- No leading / trailing / consecutive underscores in a segment.
- Decomposed identifiers don't collide with C# reserved words.

A failed decomposition produces `D2I18N001` and the key is dropped from the
emitted catalog.

**3. TKEmitter emits `TK.g.cs`.**

```csharp
public static partial class TK
{
    public static class Common
    {
        public static class Errors
        {
            public static readonly TKMessage NOT_FOUND = new("common_errors_NOT_FOUND");
            public static readonly TKMessage UNAUTHORIZED = new("common_errors_UNAUTHORIZED");
        }
    }

    public static class Auth
    {
        public static class Errors
        {
            public static readonly TKMessage INVALID_ROLE = new("auth_errors_INVALID_ROLE");
        }
    }
}
```

Each constant is a `static readonly TKMessage` carrying just the key.
Parameter substitution happens lazily at render time via `TKMessage.With(...)`.

**4. Consumer references the constant.**

```csharp
return D2Result.NotFound(TK.Common.Errors.NOT_FOUND);
return D2Result.ValidationFailed(TK.Auth.Errors.INVALID_ROLE.With("role", roleName).With("org", orgName));
```

The `TKMessage` type's `internal`-ctor design makes
`D2Result.Messages = ["untranslated literal"]` structurally unrepresentable —
every consumer is forced through `TK.*`.

**Per-source-gen details + diagnostic catalog**: see
[`server/shared/dotnet/i18n-source-gen/README.md`](../server/shared/dotnet/i18n-source-gen/README.md).

---

## §2. TypeScript — tools/ts-codegen

### §2.1. JSON specs as inputs

The TypeScript side reads the same JSON specs under `contracts/<topic>/`. Each
per-topic emitter is a `tsx` script under `tools/ts-codegen/src/`:

```
tools/ts-codegen/
├── README.md
├── package.json
├── src/
│   ├── orchestrator.ts             ← entry point; runs every emitter in dep order
│   ├── auth-context-emit.ts
│   ├── auth-error-codes-emit.ts
│   ├── auth-failures-emit.ts
│   ├── auth-scopes-emit.ts
│   ├── d2result-envelope-emit.ts
│   ├── dlq-failure-metadata-emit.ts
│   ├── encryption-domains-emit.ts
│   ├── encryption-frame-emit.ts
│   ├── error-codes-emit.ts
│   ├── grpc-trailers-emit.ts
│   ├── headers-emit.ts
│   ├── jwt-claims-emit.ts
│   ├── otel-messaging-tags-emit.ts
│   ├── problem-details-emit.ts
│   ├── request-context-emit.ts
│   ├── wire-shape-emit.ts
│   └── lib/
│       ├── string-builder.ts       ← mirrors .NET StringBuilder.AppendLine
│       ├── diagnostics.ts          ← shared diagnostic shape + formatter
│       ├── spec-loader.ts          ← JSON parse + diagnostic on failure
│       ├── file-emit.ts            ← atomic write + byte-equal short-circuit
│       └── paths.ts                ← repo-root helpers
└── tests/
```

Each emitter exports a single `runXxxEmit(force?)` function returning the
diagnostics array (empty on success). The CLI entry at the bottom of each file
invokes `runXxxEmit` and exits non-zero on diagnostic count > 0.

### §2.2. Emitter pattern

Each emitter mirrors the .NET pattern shape:

```typescript
// tools/ts-codegen/src/error-codes-emit.ts
import { loadSpec } from "./lib/spec-loader.js";
import { StringBuilder } from "./lib/string-builder.js";
import { writeGeneratedFile, isOutputUpToDate } from "./lib/file-emit.js";
import { contractsPath, tsPackagePath } from "./lib/paths.js";

export function runErrorCodesEmit(force = false): Diagnostic[] {
  const specPath = contractsPath("error-codes/error-codes.spec.json");
  const target = tsPackagePath("@d2/result", "src/generated/ErrorCodes.g.ts");

  if (!force && isOutputUpToDate(target, [specPath])) return [];

  const { spec, diagnostics } = loadSpec<ErrorCodesSpec>(specPath, "D2EC001");
  if (diagnostics.length > 0) return diagnostics;

  const sb = new StringBuilder();
  sb.appendLine("// <auto-generated />");
  sb.appendLine("// Generated from contracts/error-codes/error-codes.spec.json.");
  sb.appendLine("// Do not edit by hand.");
  sb.appendLine();
  sb.appendLine("export const ErrorCodes = {");
  for (const entry of spec.codes) {
    sb.appendLine(`  ${entry.constant}: "${entry.constant}" as const,`);
  }
  sb.appendLine("} as const;");

  writeGeneratedFile(target, sb.toString());
  return [];
}

if (import.meta.url === `file://${process.argv[1]}`) {
  const diagnostics = runErrorCodesEmit(process.argv.includes("--force"));
  if (diagnostics.length > 0) {
    diagnostics.forEach((d) => console.error(formatDiagnostic(d)));
    process.exit(1);
  }
}
```

Five load-bearing properties:

1. **`loadSpec` for parse + diagnostic.** A malformed spec returns
   `D2<TOPIC>001` consistent with the .NET side; consumers grepping CI logs
   for `D2*` find both .NET build failures and TS codegen failures.
2. **`isOutputUpToDate` mtime check.** Unchanged builds cost a stat-call per
   source, not a full parse + emit. The `--force` flag bypasses for
   spec-edit verification.
3. **`writeGeneratedFile` byte-equal short-circuit.** Atomic write only if the
   content differs. A second consecutive run produces zero diff — this is the
   idempotency contract.
4. **String-builder mirrors .NET shape.** No template engine, no AST
   manipulation. `sb.appendLine(...)` is the only writing primitive.
5. **CLI entry at the bottom.** `if (import.meta.url === ...)` only runs when
   invoked directly. `pnpm codegen` (orchestrator.ts) imports `runXxxEmit`
   programmatically.

### §2.3. Output shape

Each emitter writes one `.g.ts` file per consuming package:

```typescript
// server/web/node_modules/@d2/result/src/generated/ErrorCodes.g.ts
// (during local dev these resolve to backends/node/shared/result/src/generated/ErrorCodes.g.ts)

// <auto-generated />
// Generated from contracts/error-codes/error-codes.spec.json.
// Do not edit by hand.

export const ErrorCodes = {
  OK: "OK" as const,
  NOT_FOUND: "NOT_FOUND" as const,
  FORBIDDEN: "FORBIDDEN" as const,
  // ... 15 total entries
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];
```

Output paths land under each consuming package's `src/generated/` directory.
Files are committed to git (`linguist-generated=true` in `.gitattributes`).

### §2.4. Example walkthrough — auth-error-codes

The `auth-error-codes` catalog drives both `AuthErrorCodes` constants and the
`AuthFailures.*` semantic-helper factories on both .NET and TypeScript sides
from one spec.

**1. Author the spec.**

```json
// contracts/auth-error-codes/auth-error-codes.spec.json
{
  "codes": [
    {
      "constant": "AUTH_TOKEN_INVALID",
      "httpStatus": 401,
      "factoryName": "JwtInvalidToken",
      "userMessageKey": "auth_errors_TOKEN_INVALID",
      "category": "JWT"
    },
    {
      "constant": "AUTH_SCOPE_INSUFFICIENT",
      "httpStatus": 401,
      "factoryName": "ScopeInsufficient",
      "userMessageKey": "auth_errors_SCOPE_INSUFFICIENT",
      "category": "Authorization"
    }
  ]
}
```

**2. Both halves emit from the same spec.**

The .NET `auth-error-codes-source-gen` emits `AuthErrorCodes.g.cs` (constants
+ `AllCodes` set + `GetHttpStatus` switch + `KebabCase` helper) plus
`AuthFailures.g.cs` (semantic `D2Result` factories) into
`server/shared/dotnet/auth/Generated/`.

The TypeScript `auth-error-codes-emit.ts` emits
`@d2/auth-abstractions/src/generated/AuthErrorCodes.g.ts`. A sibling
`auth-failures-emit.ts` emits `AuthFailures.g.ts` with factories returning
`D2Result.fail(...)`.

**3. Consumer references the constants identically across languages.**

```csharp
// .NET — D2Result extension
return D2Result.AuthFailures.JwtInvalidToken();
```

```typescript
// TypeScript — same factory shape
return AuthFailures.jwtInvalidToken();
```

Cross-language wire format drift on the `d2_error_code` value is structurally
impossible — the literal string `"AUTH_TOKEN_INVALID"` comes from the same
spec field on both sides.

**Per-emitter details**: see
[`tools/ts-codegen/README.md`](../tools/ts-codegen/README.md) for the full
emitter catalog + `pnpm codegen --force` usage.

---

## §3. Adding a new spec-driven catalog

When introducing a brand-new spec-driven catalog, follow this checklist:

1. **Author the spec.** Create `contracts/<topic>/<topic>.spec.json` + a sibling
   `schema.json` describing the shape.
2. **Author the .NET source-gen** under `server/shared/dotnet/<topic>-source-gen/`:
   - csproj is `netstandard2.0`, `IsRoslynComponent`, `PrivateAssets="all"`
     on Roslyn deps + bundled `System.Text.Json`.
   - References `source-gen-shared/` files via `<Compile Include="..\source-gen-shared\**\*.cs">`
     for the netstandard2.0 polyfills + cross-source-gen records
     (`SpecFile`, `LoadResult<TSpec>`, `EmitDiagnostic`).
   - Allocates a `D2<TOPIC>NNN` diagnostic ID prefix (3-5 chars).
   - Loader (`<X>Loader.cs`) parses JSON → typed record; Emitter
     (`<X>Emitter.cs`) takes typed record → emit-result; Generator
     (`<X>Generator.cs`) is the Roslyn host wrapper with single-target dispatch
     by consuming-assembly name.
3. **Wire the consumer csproj** to reference the source-gen as Analyzer +
   declare the spec as `<AdditionalFiles>` per §1.1 above. Add the consumer to
   the sourcegen registry table in
   [`server/shared/dotnet/README.md`](../server/shared/dotnet/README.md).
4. **Author the TypeScript emitter** under `tools/ts-codegen/src/<topic>-emit.ts`:
   - Mirrors the .NET emitter shape — load spec, build string with
     `StringBuilder`, write via `writeGeneratedFile`.
   - Re-uses the same `D2<TOPIC>NNN` diagnostic IDs.
   - Register in `tools/ts-codegen/src/orchestrator.ts` if there's a dep
     ordering requirement.
5. **Author per-source-gen tests** under `tests/`:
   - `<X>EmitterTests` — feed the emitter typed specs, assert against the
     emitted source via per-VALUE substring pins (not snapshot frameworks).
   - `<X>SpecLoaderTests` — feed malformed JSON, assert the right diagnostic
     ID + message.
   - `<X>GeneratorTests` — Roslyn `CSharpGeneratorDriver` integration test
     that runs the full pipeline.
   - `<X>DiagnosticIdsTests` — assert every `D2<TOPIC>NNN` ID is declared in
     `DiagnosticIds.cs` AND `DiagnosticDescriptors.cs` (catches drift between
     the two files).
6. **Commit the emitted `.g.cs` + `.g.ts` files** to git. `.gitattributes`
   should mark them `linguist-generated=true` so they show condensed in PR
   diffs.

The cost of bringing up a new catalog is dominated by the spec design (what
fields, what catalog shape, what semantic factories). The actual codegen
plumbing is a ~200-line copy-paste from any existing sibling source-gen.

---

## References

- [`server/shared/dotnet/README.md`](../server/shared/dotnet/README.md) —
  sourcegen registry table covering all 19 `*-source-gen/` libs with one-line
  descriptions + per-source-gen README links.
- [`tools/ts-codegen/README.md`](../tools/ts-codegen/README.md) — TypeScript
  emitter catalog + library helpers + build integration + `--force` regen.
- [`server/shared/dotnet/source-gen-shared/`](../server/shared/dotnet/source-gen-shared/)
  — shared netstandard2.0 polyfills + cross-source-gen records (`SpecFile`,
  `LoadResult`, `EmitDiagnostic`) referenced by every `*-source-gen/` csproj.
- [`server/shared/dotnet/i18n-source-gen/README.md`](../server/shared/dotnet/i18n-source-gen/README.md)
  — the original SrcGen pattern this codebase mirrors elsewhere.
- [PATTERNS.md](PATTERNS.md) — high-level entry for the spec-driven codegen
  philosophy.

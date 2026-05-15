<!--
Copyright (c) DCSV. All rights reserved.
-->

# PARITY.md — Cross-Language Parity Tracking

> Template + justification framework for any cross-language additions in D²-WORX.
>
> Backend is currently **.NET only** (the SvelteKit BFF is the only non-.NET surface). This doc exists as a template + framework for future cross-language additions (e.g., a future Python ML service, mobile SDK).

---

## When Cross-Language Parity Matters

Parity tracking applies when the SAME concept must exist in MULTIPLE languages. Examples:

- A shared abstraction (D2Result, BaseHandler) implemented in both C# and TypeScript
- A wire-format type generated in multiple languages from the same proto
- A test matcher pattern duplicated in xUnit + Vitest
- A naming convention enforced consistently across languages

When a concept lives in ONE language only, parity isn't a concern — it's exclusive.

---

## "Why Exclusive?" Framework

Before adding a cross-language component, justify why it must be cross-language. Single-language exclusivity is the default; parity is the exception.

For each entry in the parity table, the **Why exclusive?** column documents the reason if a counterpart in another language is intentionally absent. Acceptable reasons:

| Reason                                 | Example                                                                                                                                                                 |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Language has built-in equivalent**   | `Microsoft.Extensions.DependencyInjection` is built into .NET — no D2-specific DI container needed                                                                      |
| **Single-language consumer surface**   | If only the SvelteKit BFF needs a primitive, it lives in TS only                                                                                                        |
| **Different ergonomic ceiling**        | Some patterns (e.g., reflection-driven `[RedactData]`) work better in one language than the other; the other can use a different approach (per-handler `RedactionSpec`) |
| **Single-language runtime constraint** | Python-only ML library, JVM-only compliance toolkit                                                                                                                     |
| **Frozen for migration**               | Component is being phased out; not worth porting to a sibling language                                                                                                  |

Unacceptable "exclusive" reasons (= force parity instead):

- "Nobody asked for the other one yet" — if both languages have the same problem, both should have the same solution
- "Implementation cost" — parity reduces long-term cost; exclusivity adds it
- "We already have one, adding another would be inconsistent" — circular reasoning; the inconsistency IS the parity gap

---

## Parity Table Template

Use this table when (re)introducing cross-language components.

| Concern            | .NET                | SvelteKit                                                                           | (Future) | Why exclusive? (per cell)                                                                          |
| ------------------ | ------------------- | ----------------------------------------------------------------------------------- | -------- | -------------------------------------------------------------------------------------------------- |
| (example) D2Result | `D2.Shared.Result`  | (SvelteKit doesn't have BaseHandler-style handlers; result type imported as needed) | TBD      | SvelteKit consumes via REST proxy; no per-handler invocation pattern                               |
| (example) Handler  | `D2.Shared.Handler` | —                                                                                   | —        | SvelteKit BFF doesn't have `BaseHandler`-style handlers (load functions instead); no parity needed |

Empty row at the bottom — fill in as cross-language additions land.

| Concern | .NET | SvelteKit | (Future) | Why exclusive? (per cell) |
| ------- | ---- | --------- | -------- | ------------------------- |
|         |      |           |          |                           |

---

## Process

When adding a cross-language component:

1. **Identify the concept** — what abstraction is being shared?
2. **Identify the consumers** — which languages have the same problem?
3. **Justify exclusivity** for any language NOT in the implementation set (using the framework above)
4. **Add a row** to the parity table above (or create a new table for the component category)
5. **Document the API contract** in shared docs — both implementations must match
6. **Verify drift** as part of the audit checklist (per `docs/AUDIT_CHECKLIST.md` — "Cross-Service" section)

---

## Anti-Patterns

- **Implementing in language A, "porting later"** — if the other language needs it, port now or document why not
- **Diverging APIs across languages** — if D2Result has a `BubbleFail` method in .NET, it has the same method in TS (with adjusted naming convention)
- **Implementing twice with subtle differences** — one of them WILL drift. Either share via codegen or document divergences explicitly.
- **Not tracking parity at all** — over time, drift accumulates silently. The table is the source of truth.

---

## Parity test infrastructure

Cross-language parity for spec-driven catalogs is enforced by a fixture-driven test pair: the .NET test suite emits canonical JSON fixtures to disk; a TS-side Vitest workspace reads those fixtures and asserts that the spec-emitted TypeScript decoders / encoders / catalogs agree byte-for-byte (after canonicalization).

**Architectural shape**

```
.NET test                                    TS Vitest test
[Trait("Category","ContractFixtures")]       (in @d2/contract-tests)
       |                                              ^
       v                                              |
  Emit fixture JSON                              Read fixture JSON
  to disk                                        from disk
       |                                              |
       └────────────► same path ◄────────────────────┘
            server/shared/typescript/contract-tests/
            fixtures/<catalog>/<scenario>.json
```

The fixture file is the meeting point. Fixtures are committed to git so PR diffs surface accidental drift. There is no subprocess, no JSON-RPC bridge, no live spawn-and-roundtrip — comparison is deterministic across two independent test runs (one per language).

**Direction**: forward-only (`.NET → fixture → TS read+assert`). Bidirectional cross-language assertion (TS-emit → .NET-read) is intentionally out of scope; any future need lands as a separate test surface.

**Catalogs covered (initial set)**

- `propagated-context/` — `IPropagatedContext` envelope + per-spec-field cap boundaries
- `auth-context/` — `IAuthContext` typed-shape property surface
- `request-context/` — `IRequestContext` typed-shape property surface (transitive `IAuthContext` + own properties)
- `jwt-payload/` — `JwtPayload` typed-shape vs spec-emitted `JwtClaimTypes` constants
- `redact-paths/` — `IAuthContextRedactPaths` / `IRequestContextRedactPaths` arrays vs `[RedactData]`-attributed properties on the .NET interfaces
- `headers/` — `CommonHeaders` / `HttpHeaders` / `AmqpHeaders` / `GrpcHeaders` `as const` membership and wire values

**Invocation**

```bash
# 1. Regenerate fixtures from the .NET side:
dotnet test server/D2.slnx --filter "Category=ContractFixtures"

# 2. Run the TS parity assertions:
pnpm test:contracts        # from repo root
```

**CI hook**

`.github/workflows/test.yml` carries commented-out CI job blocks for `contract-fixtures-emit` (regenerates fixtures + asserts no `git diff` drift) and `contract-tests-parity` (runs the Vitest assertions). They activate alongside the .NET `build` job.

**Adding a catalog**

1. Pick a synthetic-data scenario set (every fixture uses synthetic IDs / RFC 5737 IPs / `*.invalid` emails — no real PII).
2. Add a `<catalog>FixtureEmitter.cs` under `server/shared/dotnet/tests/Integration/ContractFixtures/` with one `[Fact, Trait("Category","ContractFixtures")]` per scenario; write via `FixturePathHelpers.WriteFixture`.
3. Add a `<catalog>.parity.test.ts` under `server/shared/typescript/contract-tests/tests/` that loads each scenario via `loadFixture` and asserts per-VALUE / per-PROPERTY equality.
4. Update the parent `server/shared/typescript/README.md` Mermaid graph + this doc's catalog list.

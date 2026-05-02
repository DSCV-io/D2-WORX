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

| Reason | Example |
|---|---|
| **Language has built-in equivalent** | `Microsoft.Extensions.DependencyInjection` is built into .NET — no D2-specific DI container needed |
| **Single-language consumer surface** | If only the SvelteKit BFF needs a primitive, it lives in TS only |
| **Different ergonomic ceiling** | Some patterns (e.g., reflection-driven `[RedactData]`) work better in one language than the other; the other can use a different approach (per-handler `RedactionSpec`) |
| **Single-language runtime constraint** | Python-only ML library, JVM-only compliance toolkit |
| **Frozen for migration** | Component is being phased out; not worth porting to a sibling language |

Unacceptable "exclusive" reasons (= force parity instead):
- "Nobody asked for the other one yet" — if both languages have the same problem, both should have the same solution
- "Implementation cost" — parity reduces long-term cost; exclusivity adds it
- "We already have one, adding another would be inconsistent" — circular reasoning; the inconsistency IS the parity gap

---

## Parity Table Template

Use this table when (re)introducing cross-language components.

| Concern | .NET | SvelteKit | (Future) | Why exclusive? (per cell) |
|---|---|---|---|---|
| (example) D2Result | `D2.Shared.Result` | (SvelteKit doesn't have BaseHandler-style handlers; result type imported as needed) | TBD | SvelteKit consumes via REST proxy; no per-handler invocation pattern |
| (example) Handler | `D2.Shared.Handler` | — | — | SvelteKit BFF doesn't have `BaseHandler`-style handlers (load functions instead); no parity needed |

Empty row at the bottom — fill in as cross-language additions land.

| Concern | .NET | SvelteKit | (Future) | Why exclusive? (per cell) |
|---|---|---|---|---|
| | | | | |

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

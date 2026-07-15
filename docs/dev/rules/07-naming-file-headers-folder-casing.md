<!--
Copyright (c) DCSV. All rights reserved.
-->

## 7. Naming, File Headers, Folder Casing
<a name="top"></a>
_[← rules index](../rules.md) · §7 of the D2-WORX rules catalog._

**Predicate index:** §7.1–§7.23 · 24 predicates · irregular sub-IDs: 7.7a.

### C# Naming

- **7.1** Do C# identifiers follow the convention table?

| Element                          | Convention      | Example             |
| -------------------------------- | --------------- | ------------------- |
| Classes/Records/Interfaces       | `PascalCase`    | `GetReferenceData`  |
| Methods/Properties               | `PascalCase`    | `HandleAsync`       |
| Private instance fields          | `_camelCase`    | `_memoryCache`      |
| Private readonly instance fields | `r_camelCase`   | `r_getFromMem`      |
| Private static fields            | `s_camelCase`   | `s_instance`        |
| Private static readonly fields   | `sr_camelCase`  | `sr_activitySource` |
| Static readonly (non-private)    | `SR_PascalCase` | `SR_ActivitySource` |
| Private constants                | `_UPPER_CASE`   | `_BATCH_SIZE`       |
| Public/Internal constants        | `UPPER_CASE`    | `MAX_ATTEMPTS`      |
| Local constants (tests)          | `snake_case`    | `expected_count`    |
| Local variables                  | `camelCase`     | `result`            |

- **Carve-out**: handlers using **primary constructors** — constructor parameters do NOT take `r_` prefix (they're parameters, not fields, even though they're accessed like fields inside the class body). The carve-out applies ONLY to handler primary-constructor parameters; regular fields keep their prefixes.
  - Evidence: per new field / property / class → convention check.

- **Test-local naming clarification (avoiding the most common slip)**: in test code, `snake_case` is permitted ONLY for `const` declarations (e.g. `const int expected_count = 5;`). NON-`const` test locals — `var foo = ...`, `string[] foo = ...`, `out var foo`, etc. — MUST use `camelCase` per the "Local variables" row of the table above. Examples:
  - ✅ `const int expected_count = 5;` (test-local const → snake_case)
  - ✅ `var sessionId = Guid.NewGuid();` (test-local non-const → camelCase)
  - ❌ `var session_id = Guid.NewGuid();` (snake_case is for consts only)
  - ❌ `out var claim_value` (out-vars are not consts → camelCase)
  - **Why**: the `_` marks compile-time-inlined constants; `snake_case` on mutable / out-bound locals reads ambiguously and dilutes that signal.

### TypeScript Naming

- **7.2** Do TypeScript identifiers follow: `camelCase` for variables/functions, `PascalCase` for types/classes/interfaces/components, `kebab-case` for module file names?
  - Evidence: per new identifier / file → convention check.

### Folder casing

- **7.3** Are folders OUTSIDE a project (csproj-grouping, organizational) lowercase / kebab-case for multi-word? (`server/`, `services/`, `edge/`, `app/`, `client/`, `dotnet/`, `problem-details/`, `source-gen-shared/`, `service-defaults/`, `infra/`, `tools/`, `docs/`)
- **7.4** Are folders INSIDE a project (namespace-mapping, where Rider auto-creates folders from namespace operations) PascalCase? (`Application/`, `Infrastructure/`, `Handlers/`, `Commands/`, `Queries/`, `Entities/`, `ValueObjects/`, `Rules/`, `Persistence/`, `Messaging/`, `Postgres/`, `RabbitMq/`) For a SERVICE project the in-project folder set is governed by the structure standard (§9.24 + [ADR-0020](../../public/docs/adrs/0020-service-project-structure.md)); this predicate is only the *casing* check — the killed letter-tier / mirror-tree / `Models/` / `CQRS/` folder names are §9.24 FINDINGS, not casing exceptions.
- **7.5** Are `.cs` file names PascalCase (matching the type they contain — one-class-per-file)?
- **7.6** Are `.csproj` file names PascalCase, dot-separated (`DcsvIo.D2.Handler.csproj`) — the csproj filename IS the assembly name?

> **The rule**: if Rider auto-generates a folder from a namespace operation, that folder must be PascalCase. Anything else is lowercase.

### File headers

- **7.7** Does every source file you created or modified carry the standard copyright header for its language? Files that don't support comments (`.json`, `.lock`, `.snap`) and machine-generated files (EF migrations, paraglide compile output, proto-codegen, husky shims, JetBrains `.idea/`, lock files) are exempt.
  - **Scope boundary vs §24.13.4 (prose-grep file scope)**: §7.7's exemption applies to HEADER-PRESENCE only — comment-less files like `.json` cannot carry a header. It does NOT exempt the file's PROSE CONTENT (`$note` / `description` / `doc` string fields in `public/contracts/**/*.json` + `private/contracts/**/*.json`) from §11.x / §14.x prose-framing predicates, which §24.13.4 pulls into the prose-grep scope. The two are orthogonal: §7.7 governs file-level headers; §24.13.4 governs string-field-level prose.

#### Header forms by comment family

**`//` line comments** — C#, TypeScript, JavaScript, Proto, Go, Rust, Java (`.cs`, `.ts`, `.tsx`, `.js`, `.cjs`, `.mjs`, `.proto`, `.go`, `.rs`, `.java`):

For C# (`.cs`) — StyleCop SA1633 enforces XML `<copyright>` element:

```csharp
// -----------------------------------------------------------------------
// <copyright file="FileName.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------
```

Everything else in this family:

```ts
// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
```

**`/* */` block comments** — CSS / SCSS / LESS:

```css
/* -----------------------------------------------------------------------
 * Copyright (c) DCSV. All rights reserved.
 * ----------------------------------------------------------------------- */
```

**`#` line comments** — Bash, YAML, Dockerfile, PowerShell, Makefile, Grafana Alloy, env files, gitignore-family, editorconfig, npmrc, prettierignore, TOML, INI, Python, Ruby, R.

For shebang-bearing files, shebang stays on line 1; header follows:

```bash
#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------
```

For files without shebang, header is line 1.

**`<!-- -->` HTML-comment block** — Markdown, HTML, Svelte, Vue:

```markdown
<!--
Copyright (c) DCSV. All rights reserved.
-->
```

For `.svelte` / `.vue`, the header lives at the very top of the file before `<script>` / `<template>`.

**`<!-- -->` XML-comment block** — XML, csproj, slnx, props, targets:

```xml
<Project>
  <!--
  Copyright (c) DCSV. All rights reserved.
  -->
  ...
</Project>
```

(or before the root element when an `<?xml ... ?>` declaration is present)

**`--` line comments** — SQL, Lua, Haskell, Ada:

```sql
-- -----------------------------------------------------------------------
-- Copyright (c) DCSV. All rights reserved.
-- -----------------------------------------------------------------------
```

Evidence: per new/modified file → header line 1 confirmed (or shebang + line 2).

#### Adding a new language

If you encounter a language not listed above and it supports comments, the header content stays `Copyright (c) DCSV. All rights reserved.` for **private / monorepo-root** surfaces — only the comment delimiter changes. For files under `public/**`, use the dual-header public form (§7.7a).

- **7.7a** **Dual-header law (public Apache / private ARR).** Do files under `public/**` carry Apache-2.0 headers matching `public/stylecop.public.json` `copyrightText` (`Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.`) with **no** “All rights reserved” — across residual surfaces (md/ts/tsx/csproj/props/targets/yaml/yml/proto/slnx + any other hand-authored public ARR surface), not extension-list-only? Do files under `private/**` and monorepo-root private KEEP use proprietary ARR headers (`Copyright (c) DCSV. All rights reserved.`)? Is public LICENSE packing `public/LICENSE` (Apache-2.0)? Do residual greps exclude `coverage/**`, `node_modules/**`, `**/Generated/**`, `bin/**`, `obj/**`, `dist/**`?
  - **C# / StyleCop stamp:** public `.cs` headers must match StyleCop SA1633 AdditionalFiles form already used on public packages (`// <copyright file="…" company="DCSV">` + Apache `copyrightText`). Do **not** invent an SPDX-only banner that fails SA1633. Already-Apache public `.cs` is re-verify only — do not thrash clean headers.
  - **Comment-family adaptation:** markdown / HTML / proto / YAML / TS / XML project files adapt comment syntax only; wording stays the StyleCop `copyrightText` (public) or ARR (private).
  - **Generated:** never hand-edit `.g.*` or `**/Generated/**` — fix the generator or header template if public generators stamp ARR.
  - **Evidence:** `All rights reserved` under `public/` (with excludes above) → 0 outside documented third-party/vendored allowlist; report residual counts by extension/surface including proto, slnx, cs (expect 0 ARR), and catch-all. Private ARR residual under `public/` = FINDING-HIGH.
  - **Why:** export surface is Apache-2.0; ARR headers on public files misstate the license and fail OSS consumers / StyleCop dual configs. Dual-header prevents inventing a third SPDX-only form that conflicts with `stylecop.public.json`.
  - **How:** bulk-rewrite residual public ARR headers to the locked StyleCop stamp; keep private monorepo on ARR; re-verify public `.cs` only.

### Translation key naming

- **7.8** Do translation keys follow the convention?
  - Auth pages: `auth_{feature}_{purpose}` (e.g., `auth_sign_in_title`)
  - App pages: `webclient_app_{page}_{purpose}` (e.g., `webclient_app_profile_title`)
  - Design/demo/debug: `webclient_{section}_{purpose}` (e.g., `webclient_debug_session_title`)
  - Common UI/errors: `common_ui_*` / `common_errors_*`
  - Backend handler messages: use `common_errors_*` keys where possible
  - Reuse existing keys where they match
  - Evidence: per new key → convention check.

### Scope vs Permission terminology

- **7.9** Does code use **"scope"** as the primary term throughout (not "permission")? JWT carries them as the OAuth-canonical `scope` claim (space-separated string). Code references them as constants in `DcsvIo.D2.Auth.Scopes`.
  - Evidence: per new code touching authz → "scope" terminology.

### Git conventions

- **7.10** Do branch names follow the prefix convention? `feat/...`, `fix/...`, `docs/...`, `refactor/...`, `test/...`, `infra/...`, `chore/...`, `ci/...`.

- **7.11** Do commits use conventional-commit format with scope? (`feat(edge): add primary locales`).

- **7.12** Are `Co-Authored-By` lines absent? (Enforced by `.husky/commit-msg` hook; will reject if present.)

- **7.13** Are markdown tables in committed docs aligned for plain-text readability?
  - Evidence: per touched markdown table → alignment check.

### Universal style (applies across all source + KEEP docs)

- **7.14** Are lines ≤ 100 chars in `.cs` / `.ts` / `.tsx` source?
  - **Apply to**: human-authored source code, XML doc summaries, parameter lists, string literals.
  - **Wrap strategies**: break long XML doc summaries onto multiple lines; split long parameter lists across lines; break long string literals into concatenations or interpolations across lines; extract long expressions into named locals.
  - **Allowlist**: rare unbreakable long URLs / connection strings / encoded strings — note the reason in the surrounding comment (`// long URL — cannot wrap`).
  - **Carve-out — auto-generated source is EXEMPT**: `.g.cs` / `.g.ts` files + any committed source-generator output (e.g. under `Generated/`) are NOT subject to the 100-char rule — generated catalogs are consumed via IntelliSense, not read as source. Emitters MUST NOT carry wrap helpers (`WrapWords` / `MAX_LINE_LENGTH`) just to satisfy this predicate on generated output — that infrastructure is dead weight + flaky snapshot pins.
  - **Carve-out — the limit is on CODE lines, NOT string-literal CONTENT**: the 100-char ceiling governs the structure of a line of code — its declarations, expressions, parameter lists, and call chains. It does NOT govern the human-readable CONTENT inside a single string literal. A test description string (`it("…")` / `describe("…")` / a `[Fact(DisplayName = "…")]`), a diagnostic-message / `paramMessage` template, and a test-data string literal are EXEMPT when the literal itself runs past 100 chars. Breaking such a literal to satisfy the column count HURTS readability and breaks grep/searchability, and Prettier leaves a long literal intact anyway. The exemption is for the literal's CONTENT only: the surrounding code (the function call, the assignment, the array) still wraps normally, and a literal that is genuinely a concatenation of code-level fragments is NOT exempt. When a long literal sits on its own line, that line is allowed to exceed 100 chars.
  - **Carve-out — a `using`-alias directive is EXEMPT**: a `using X = <fully-qualified-type>;` alias is not subject to the ceiling — the fully-qualified target is indivisible (no legal wrap), and a mandatory convention (§5.29 handler `H` / `I` / `O` aliases) can force a path over 100 chars (the longest KeyCustodian aliases hit 123). Accepted as-is.
  - **Why**: visual scannability + sane review diffs on a 13" laptop. The string-content carve-out keeps that goal honest — a wrapped message literal is LESS scannable and LESS greppable than one long line, so the column rule would work against its own purpose on literal text.
  - Evidence: `awk 'length > 100' <new/modified .cs/.ts files — EXCLUDING .g.cs / .g.ts / Generated/**>` returns expected/empty — modulo lines whose only over-length content is a single string literal (test description, diagnostic-message template, or test-data string), which are accepted. 60+ such instances already live in-tree (test descriptions + message templates) and pass both Prettier and the build.

- **7.15** Is all spelling American English (no British / Canadian variants)?
  - **Apply EVERYWHERE**: comments, doc strings, identifier names, README text, log messages, error messages, test names, commit messages.
  - **Common splits**:
    - `behavior` not `behaviour` (and `behaviors`, `behavioral`)
    - `color` not `colour` (and `colors`, `colored`, `coloring`)
    - `analyze` not `analyse` (and `analyzed`, `analyzing`, `analyzer`, `analysis` is identical)
    - `honor` not `honour` (and `honored`, `honoring`)
    - `canceled` not `cancelled` (single L — matches BCL `OperationCanceledException`); `canceling` not `cancelling`; `cancellation` is identical (double L is correct here)
    - `favorite` not `favourite`
    - `defense` not `defence`
    - `recognize` not `recognise` (and `recognized`, `recognizing`)
    - `optimize` not `optimise` (and `optimized`, `optimizer`, `optimization`)
    - `organization` not `organisation` (and `organize`, `organized`)
    - `prioritize`, `customize`, `categorize`, `utilize`, `realize`, `minimize`, `maximize`, `emphasize`, `criticize`, `summarize`
    - `program` not `programme`
    - `modeled`, `modeling` (single L) — not `modelled`, `modelling`
    - `signaled`, `signaling`, `labeled`, `labeling`, `traveled`, `traveling`
    - `neighbor` not `neighbour`
    - `materialize` not `materialise` (and `materialized`, `materializing`, `materialization`)
    - `catalog` not `catalogue` (and `catalogs`, `cataloged`, `cataloging`)
    - `serialize`, `centralize`, `specialize`, `standardize`, `finalize`, `initialize`, `harmonize`, `pressurize` (and conjugations) — not the `-ise` forms
    - `defense`, `license` (verb), `practice` (verb) — `-se` not `-ce`
  - **Allowlist**: proper nouns, third-party identifiers (e.g. a UK org's name), quoted user content. Note inline (`// proper noun — keep British spelling`). The `en-GB.json` locale file is exempt — by definition.
  - **Audit grep**: enumerate root + conjugations (`-e/-ed/-es/-ing/-ation/-able/-er`). Bare `\b<root>\b` is INSUFFICIENT — word boundaries reject the conjugated forms (`\brecognise\b` does NOT match `recognised`). Use:
    ```
    grep -rEn '\b(analys(e|ed|es|ing|er)|behaviour(s|al|ally)?|cancell(ed|ing)|catalogu(e|es|ed|ing)|categoris(e|ed|es|ing|ation)|centralis(e|ed|es|ing|ation)|colour(s|ed|ing|ful)?|customis(e|ed|es|ing|ation|able)|defence|emphasis(e|ed|es|ing)|favour(s|ed|ing|ite|ites|able)?|finalis(e|ed|es|ing|ation)|harmonis(e|ed|es|ing|ation)|honour(s|ed|ing|able)?|initialis(e|ed|es|ing|ation)|labell(ed|ing)|licence(s)?|materialis(e|ed|es|ing|ation)|maximis(e|ed|es|ing|ation)|minimis(e|ed|es|ing|ation)|modell(ed|ing)|neighbour(s|hood|ing)?|optimis(e|ed|es|ing|ation|er)|organis(e|ed|es|ing|ation)|practis(e|ed|es|ing)|pressuris(e|ed|es|ing|ation)|prioritis(e|ed|es|ing|ation)|programme(s)?|realis(e|ed|es|ing|ation)|recognis(e|ed|es|ing|able)|serialis(e|ed|es|ing|ation|er)|signall(ed|ing)|specialis(e|ed|es|ing|ation)|standardis(e|ed|es|ing|ation)|summaris(e|ed|es|ing)|synchronis(e|ed|es|ing|ation)|travell(ed|ing)|utilis(e|ed|es|ing|ation))\b' <scope>
    ```
    `cancell(ed|ing)` deliberately excludes `cancellation` (double-L is correct in American English for that one noun). `defence` / `licence` are root-only (no common conjugations differ from American forms).
  - Evidence: per-scope grep result.

- **7.16** Are comments minimal? Default to writing **NO comments**. Add one only when the WHY is non-obvious — a hidden constraint, a subtle invariant, a workaround for a specific bug, behavior that would surprise a reader.
  - **Forbidden**:
    - Explaining WHAT the code does (well-named identifiers do that — `_userMaxRetries = 3` doesn't need `// max retries for user`)
    - Referencing the current task / fix / callers (`// used by X`, `// added for the Y flow`, `// handles the case from issue #123`) — those belong in the PR description and rot as the codebase evolves
    - Multi-paragraph docstrings on non-public symbols
    - Multi-line comment blocks (>1 line) for trivial explanations
    - Commented-out code (delete it; git remembers)
    - Conversation-scoped IDs (`// F2_ regression`, `// audit decision X`) — see §14
  - **Allowed**:
    - One short line max for non-obvious WHY
    - XML doc comments on public APIs (per §5.18)
    - Long-form `<remarks>` blocks on public APIs explaining edge cases / invariants / when-to-call-vs-not
    - Bucket classification on `[GeneratedRegex]` (per §5.20)
  - Evidence: per new/modified file → comment audit (every comment justifies its existence).

- **7.17** Are commit messages following conventional-commit format AND describing the "why" (not just the "what")?
  - **Format**: `type(scope): summary`. Types: `feat`, `fix`, `docs`, `refactor`, `test`, `infra`, `chore`, `ci`. Scope: lib / service / area touched.
  - **Body** (when needed): explains motivation, trade-offs, what alternatives were rejected, what to watch for. Wrap at 72 chars in body.
  - **Forbidden**: `Co-Authored-By` lines (enforced by husky hook).
  - Evidence: per commit → format + body audit.

- **7.18** Is the commit `type` correctly classified? Confusing `fix` with `chore` / `refactor` is the most common slip.
  - `feat` — wholly new feature / capability that didn't exist before.
  - `fix` — something was BROKEN (incorrect behavior, exception, security flaw, regression) and is now fixed. Reserved for actual bug fixes.
  - `chore` — dependency removal, version bumps, pipeline consolidation (e.g. removing direct Loki sink in favor of Alloy-only), config cleanup, tooling updates. NOT a bug fix even if it removes a problematic dep.
  - `refactor` — restructuring without observable behavior change (rename, file move, extract method, change internal data structure, no new feature, no bug fix).
  - `docs` — documentation-only changes (README, comments, doc files).
  - `test` — test-only changes (adding coverage, refactoring tests, no production code change).
  - `infra` — infrastructure / deployment / Docker Compose / CI runtime / observability stack.
  - `ci` — CI workflow / GitHub Actions / pre-commit hook config (NOT runtime infra).
  - **When in doubt**: lean `chore` over `fix`. `fix` should be defensible as "something user-observable was broken; now it isn't."
  - Evidence: per commit → type classification justified.

- **7.19** When creating a PR, does the body follow `.github/pull_request_template.md` (Summary / Changes / Details / Testing / Checklist sections)?
  - **Why**: reviewers (including any GitHub Copilot review bot) expect the standard format; deviating creates friction.
  - **How**: read `.github/pull_request_template.md` first; fill in each section; use the predefined Changes list (Documentation / New feature / Bug fix / etc.) rather than freeform bullets.
  - Evidence: PR body matches template structure.

### Service project shape (the structure standard)

- **7.20** Does a standalone service take the full five-project shape, and does a module-within-host correctly take the carve-out? (Canonical: [ADR-0020](../../public/docs/adrs/0020-service-project-structure.md); cross-ref §9.24.)
  - **Standalone service** (`Courier`, `Notifications`, `Files`, `Audit`, a future `Payments`) = five runtime projects (`domain` / `app` / `infra` / `api` / `tests`) + consumer-facing `client/` + (when it owns a per-domain error-code spec) a `netstandard2.0` source-gen shell. The `api/` project (`Microsoft.NET.Sdk.Web`) is the composition root (`Program.cs` + host wiring + transport adapters + transport mappers).
  - **Module-within-host** (KeyCustodian, the auth module — both inside Edge) = the standard `domain`/`app`/`infra` **but omits `api/` and its own `tests/`**: the host's `api/` is the composition root (the module exposes `AddD2<Module>()` as its only api-shaped surface; the host calls it from `Program.cs`), the host's api does the module's transport mapping (§9.28 surface 1), and the module's tests live in the host's test project under a `<Module>/` subtree. The carve-out is **explicit and named** — the five-project shape is the default; a module never silently becomes the default.
  - **Promotion trigger**: a module is promoted to a full standalone service the moment it needs an independent deployable, an independent database lifecycle, or independent scaling — at which point it gains its own `api/` + `tests/` and `domain`/`app`/`infra` carry over unchanged.
  - **Why**: a standalone service's `Program.cs`, transport adapters, and wire mappers ship + deploy + integration-test with it and are the seam every cross-service caller hits — declaring them out-of-scope leaves every service to re-derive the same wiring. The carve-out keeps a host-embedded module's composition + transport + tests with the host that composes it.
  - Evidence: per service → project set matches its classification (standalone = 5 + clients; module = 3, no own `api`/`tests`); a module-within-host that grew its own `api/` or `tests/` without the promotion trigger = FINDING; a standalone deployable missing `api/` = FINDING. Per module that has grown its own `api/` or `tests/` — confirm the promotion trigger (own deployable, own DB lifecycle, or own independent scaling) is documented in the owning deliverable's ADR or journal; an `api/` present with no documented trigger = FINDING-L.

- **7.21** Is every DTO either co-located with its operation (`<Op>Input`/`<Op>Output` in the op folder) or, when shared by 2+ operations, promoted to a domain VO — and is there NO flat DTO bucket (`app/Models/` or equivalent)?
  - **Forbidden**: a flat `Models/` (or `Dtos/` / `Contracts/` shape-bucket) folder in the app project collecting inputs/outputs/projections with no per-shape owner; a wire/proto-mirroring DTO hand-written in app (that is a §26.1 codegen violation — it is generated into source-gen internals).
  - **Required**: an operation's input/output and any operation-private record live in the op folder (suffixed `<Op>Input`/`<Op>Output`/`<Op><Role>`, or a `private` nested type); a shape returned by 2+ operations earns domain residency as a VO (or a `Rules/` projection target).
  - **Why**: a flat DTO folder is where shapes lose their owner — a reader cannot tell which operation owns which record; co-location (or promotion to domain) gives every shape one home. Cross-ref §9.24, §26.1.
  - Evidence: grep the app project for a `Models/` / `Dtos/` folder → expect zero; per DTO → home is its op folder or a domain VO.

- **7.22** Do service-project folder names use the structure-standard concern vocabulary, with a MANDATORY tech/vendor/protocol subfolder under every `infra/` concern (even a sole impl), and does the namespace keep the `.App`/`.Infra` layer segment verbatim?
  - **Concern vocabulary**: `app/Infrastructure/` + `infra/` group by capability concern — a **PascalCase singular capability noun** (`Persistence`, `Messaging`, `Email`, `Sms`, `Realtime`, `Storage`, `Outbound`, `Vault`, `Scheduling`, `Configuration`, `RateLimiting`, `WhoIs`, …). `Vault/` not `Secrets/` — a `Secrets/` folder collides with the universal `secrets/` key-material convention on case-insensitive filesystems. The noun set is open-but-deliberate: a new concern noun is a standard amendment ([ADR-0020](../../public/docs/adrs/0020-service-project-structure.md) + [PATTERNS.md](../PATTERNS.md#service-project-structure)), not an ad-hoc per-service coinage. The generic `Providers/` wrapper is forbidden.
  - **Mandatory subfolder**: every `infra/<Concern>/` carries a tech/vendor/protocol subfolder — `infra/Persistence/Postgres/`, `infra/Messaging/RabbitMq/`, `infra/Email/Resend/`, `infra/Outbound/Grpc/` — **even when only one implementation exists today**. `infra/Persistence/SomeAdapter.cs` directly under the concern (no vendor subfolder) = FINDING.
  - **Namespace keeps the layer segment**: the namespace is the folder path verbatim INCLUDING `.App`/`.Infra` (`D2.<Area>.<Service>.App.Infrastructure.Persistence` for a port vs `D2.<Area>.<Service>.Infra.Persistence.Postgres` for an adapter) — NOT collapsed via a `RootNamespace` trick that drops the layer segment.
  - **Why**: the mandatory subfolder is the seam a second vendor lands on without a reshuffle (consistency beats the marginal nesting cost). In a service the layer IS semantics — collapsing `.App.Infrastructure` saves one segment but forces every reader to memorize a folder-vs-namespace mismatch.
  - Evidence: per `infra/` concern folder → a vendor/tech/protocol subfolder is present (FINDING if an adapter sits directly under the concern); per service `.cs` namespace → the `.App`/`.Infra` segment matches the folder; grep for a `Providers/` folder → expect zero.

- **7.23** Is every test-only / fixture-only symbol's OWN name (the leaf identifier, not merely its enclosing namespace) carrying a clear `Fixture` / `Fake` / `Stub` / `Test` / `Sample` marker — across fixtures, test doubles, seams, harnesses, AND generated fixture DTOs / proto messages / enums / models / handler interfaces / gRPC services / clients / dispatchers / route registrations? A production-looking leaf name, or a name distinguished only by its namespace, is a FINDING.
  - **Evidence**: per fixture-generated or fixture-hand-authored symbol → its leaf name contains a marker token. Pre-flight grep over the fixture-generated set + the fixture `.tsp` sources for bare production-shaped leaves (e.g. `\b(Sign|Enum|PlaceOrder|DeepNest|Temporal|Order|Session)(Input|Output|Line|Widget|Part)\b` without a `Fixture` segment; bare enum names `\b(KeyKind|Level|Status|AccountKind)\b`) → expect zero in the fixture set.
  - **Why**: a symbol is read at its USE site, where the namespace is elided by a `using` / `import` — a bare `SignOutput` or `KeyCustodianSigner` is then indistinguishable from a production type, which is how a fixture squats real wire identity and a reader mistakes a test shape for a shipping one. The marker makes test-only status self-evident wherever the symbol appears.
  - **How**: in a codegen pipeline the marker is driven from the `.tsp` (or schema) SOURCE — rename the op / model / enum / `@d2GrpcMethod` / `@d2ServedBy`, regenerate, NEVER hand-edit the `.g.*` (§26.5). Use a `<Family>Fixture<Role>` leaf convention (op `signFixture` → `SignFixtureInput`, model `OrderLine` → `OrderFixtureLine`, enum `KeyKind` → `FixtureKeyKind`). Carve-out: SHARED real types a fixture merely CONSUMES (canonical wire composites in `public/contracts/typespec/common/` / product peers under `private/contracts/typespec/`, `D2Result`, `D2Generated*` seams) are NOT fixtures and keep their real names. Cross-ref §26.5, §7.1.

<sup>[↑ jump to top](#top)</sup>

---


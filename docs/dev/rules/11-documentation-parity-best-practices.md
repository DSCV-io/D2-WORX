<!--
Copyright (c) DCSV. All rights reserved.
-->

## 11. Documentation Parity & Best Practices
<a name="top"></a>
_[← rules index](../rules.md) · §11 of the D2-WORX rules catalog._

**Predicate index:** §11.1–§11.47 · 49 predicates · irregular sub-IDs: 11.30.1, 11.35.1.

Doc drift is constant unless the doc edit ships in the SAME change as the code edit. This category covers BOTH keeping docs in sync with code (parity) AND writing docs that are useful (structure, style, accuracy, brevity, anti-pattern absence).

### Definition — "KEEP doc"

"KEEP doc" = **long-lived documentation that lives with the code in production and is read by developers consuming or maintaining it**. KEEP docs describe **current reality**, not the development journey.

**IN the KEEP-doc surface** (in scope for §11.x / §14.x):

- READMEs at every layer: parent overview (`public/packages/dotnet/README.md`, `public/packages/typescript/README.md`), per-csproj / per-package, per-service.
- Cross-cutting framework docs: `docs/PATTERNS.md`, `docs/PARITY.md`, `docs/TESTS.md`, `docs/SRC_GEN.md`, `docs/dev/process.md`, `docs/dev/rules.md`.
- Source comments of every form: `//` + `/* */` + xmldoc `///` (`.cs`, `.ts`, `.tsx`, `.svelte`, `.css`, `.go`, `.py`); shell `#`; `<!-- -->` in `.md` / `.csproj` / `.cshtml`.
- Spec / contract JSON `$note` / `description` / `doc` / `$comment` strings (`public/contracts/**/*.json` + `private/contracts/**/*.json` — these propagate to consumer docs + sometimes verbatim into codegen output).
- Test method names + describe-block titles (visible in runner output, CI dashboards, failure reports).
- Generated-code source-of-truth surfaces: emitter source (`public/tools/ts-codegen/src/**/*.ts`, SourceGen `.cs`) whose prose propagates verbatim into committed `.g.ts` / `.g.cs`; csproj / props / targets XML comments.
- Reference-data file headers: `$note` / `lastEditedAt` / `catalogVersion` in `public/contracts/geo/*.spec.json` + similar.
- AGENTS.md / agent-facing docs (with §11.9 exemptions — KEEP docs MUST NOT cite AGENTS.md, but AGENTS.md is itself a KEEP-equivalent surface for §11.x / §14.x purposes).

**NOT a KEEP doc** (allowlisted OUT of §11.x / §14.x — these are EXPLICITLY phase / dev-tracking artifacts):

- **`private/docs/v2/`** — architectural roadmap + per-phase tracking (V2.md, PHASE\_\*.md). Phase verbiage here is the point.
- **`docs/dev/deliverables/`** — immutable per-deliverable snapshots; their content IS the historical narration of how that deliverable shipped.
- **`docs/wip/`** — gitignored per-deliverable workspaces; phase / round / amendment / TODO framing is free here.
- **`docs/archive/`** — historical snapshots, preserved verbatim.
- **`MEMORY.md`**, **`CHANGELOG.md`** — temporal / journey content by nature.

**KEEP-doc framing rules** (the spirit §11.9, §11.19, §11.20, §11.28, §14.1, §14.3 enforce): describe what IS in present tense (never "will be" / "planned for" / "future X lib's job"); describe current state, not the journey (never "previously hand-written" / "we used to" / "migrated from" / "v1's old approach"); describe the convention directly (never cite AGENTS.md sections, phase numbers, audit-round / decision / deliverable IDs from inside a KEEP doc); write for a future reader who knows nothing about the project's development state — every sentence should still be true in three years. The forward-looking / historical-narration content belongs in the allowlisted dev-tracking docs above; the SURFACE (README / comment / `$note` / xmldoc) doesn't change the rule.

### Predicates — §11 documentation parity

- **11.1** Are doc edits in the SAME change as the code edits (not a separate commit)?
  - Evidence: per code change → corresponding doc change in same commit.
  - **Within-step ordering**: code → tests → verify tests pass → THEN docs (in the same commit as the code, not a separate follow-up). Docs reflect the FINAL code state, which often shifts during implementation; "same commit" means same change, NOT "docs first." Rationale per `feedback_docs_after_tests`.

- **11.2** Do telemetry tag enumerations / counter lists / metric tables in READMEs match the code in this scope?
  - Evidence: per telemetry-related doc → enumeration matches `Counter.Add` call sites.

- **11.3** Does the per-lib `README.md` public-API list match the actual public surface (no removed methods listed, no added methods missing)?
  - Evidence: per lib → API list diff vs `public` symbol scan.
  - **Includes ALL code references** — every code reference in a KEEP doc must resolve to its cited location in current HEAD:
    - **Filenames** — every `*.cs` / `*.ts` / `*.svelte` / `*.json` / `*.csproj` path must exist. Codegen output paths (`obj/Generated/...`, `*.g.cs`, `*.g.ts`) are drift-prone — a source-generator rename silently breaks every consuming README's "File layout" table with no build error.
    - **Line citations** — every `file.cs:NN` citation points at the cited line in current HEAD (or a range still containing the cited symbol / behavior). Stale line numbers signal the doc was last touched against an older code state.
    - **Fully-qualified symbol names** — every cited class / method / property / field / enum value / constant (e.g. `ITieredCache.GetOrSetAsync`, `BaseHandler<TSelf,TInput,TOutput>`) resolves via Grep / LSP against current HEAD.
    - **Package names** — every `@dcsv-io/d2-X` / `DcsvIo.D2.X` / `D2.<Service>.<Layer>` exists in the workspace (`pnpm-workspace.yaml` for TS, `D2.slnx` for .NET).
  - Audit grep: enumerate every token matching `` `*.cs` ``, `` `*.ts` ``, `` `DcsvIo.D2.* ``, `` `@dcsv-io/d2-* `` in touched READMEs → confirm each still resolves to what the doc claims. Any unresolved reference is a finding.
  - **Includes Required helpers**: a per-lib README enumerating "Public API" / "Required helpers" cross-references §5 / §16 — every "Required" / "Use this not that" helper has a section. A lib publishing `Falsey()` / `TryParseTruthyNull` / `[RedactData]` cannot omit them from its README (the discovery surface for new consumers).

- **11.4** Was [PATTERNS.md](../PATTERNS.md) updated for any new pattern introduced (handler / service-structure / DI registration / `D2Result` factory usage / RedactionSpec / mapper / repo pattern)?
  - Evidence: per pattern introduced → PATTERNS.md edit.

- **11.5** Was the relevant doc per AGENTS.md §3.5 Doc Update Map updated for cross-cutting changes? AGENTS.md §3.5 is the canonical doc-mapping source — every entry in that map is in scope for this predicate.
  - **Why**: the cross-cutting doc set evolves; enumerating it inline drifts the moment AGENTS.md §3.5 changes, so this predicate points there rather than restating.
  - Evidence: per change → matching AGENTS.md §3.5 Doc Update Map row → doc edit landed in the same commit per §11.1.

- **11.6** Mermaid dep-graph parity for `<ProjectReference>` edits. See §11.29 for the canonical generalized predicate; this row is the §11 cross-pointer covering the specific .NET parent-overview README case. Walk §11.29 for evidence.

- **11.7** When project tracking state changes (status update, open question, tracked issue, scope decision), is the current tracking doc (presently `private/docs/v2/V2.md`) updated?
  - Evidence: per tracking change → doc edit.

- **11.8** When an architectural decision overrides a prior plan, is the current tracking doc (presently `private/docs/v2/V2.md`) updated AND a new ADR added per `public/docs/adrs/README.md`?
  - Evidence: per overriding decision → tracking-doc entry + ADR entry.

- **11.9** Does any KEEP doc / README / source comment cite "AGENTS.md §X" or reference `PHASE_*.md` / `V2.md` from outside `private/docs/v2/`?
  - **Why**: KEEP docs describe current code reality; AGENTS.md is agent/workflow direction and `PHASE_*.md` / `V2.md` are forward-looking, so citing them as code canon from a KEEP doc muddles the descriptive / directive distinction.
  - Evidence: `grep -rEn 'AGENTS\.md\|PHASE_[0-9_]*\.md\|V2\.md' <scope KEEP files>` → expect zero (modulo the carve-out below).
  - **CARVE-OUT — forward-pointer when no shipped canonical exists yet**: when a pattern / library / subsystem has design content in `private/docs/v2/PHASE_*.md` but no shipped KEEP-doc canonical home exists yet (the lib isn't built, so its README doesn't exist), a KEEP doc MAY cite `private/docs/v2/PHASE_*.md` as the design home **PROVIDED** the citation carries the disambiguation framing naming all three of: not-yet-shipped status, current design path, future shipped home (e.g. `Canonical: not yet shipped; design at <path>. Will migrate to <shipped-home> when <thing> ships.`). Without the framing → §11.9 violation; with it → PASS (with a §11.1 re-audit trigger when the shipped canonical emerges).
  - **Why the carve-out exists**: sometimes the design IS the only thing that exists; forbidding all citations would force deleting the entry (loses reader value), inlining the full design (a drift class against `PHASE_*.md`), or scaffolding an empty stub (churn). The carve-out preserves directory-pointer value while keeping the citation honest about its forward-looking nature.
  - **How**: write the disambiguation framing inline at the citation site (not a footnote); when the shipped canonical later ships, update the citation in the same change per §11.1. Auditors under the carve-out: (a) check every `PHASE_*.md` citation outside `private/docs/v2/` for the framing (absence = violation, presence = PASS); (b) for each carve-out PASS, note whether the shipped canonical has since emerged (if yes → §11.1 migration update due).
  - **META-DOC ALLOWLIST**: cross-references WITHIN the meta-doc set — `docs/dev/rules.md`, `docs/dev/process.md`, `AGENTS.md`, `.github/copilot-instructions.md` — OR from those meta-docs OUT to `private/docs/v2/*` tracking docs are exempt from §11.9 (structural cross-refs in the framework-direction layer, not canonical-source claims; same precedent as the §14.1 / §14.3 meta-doc empirical-citation allowlist). KEEP docs (PATTERNS.md, per-lib / per-service READMEs, framework cross-cutting docs) do NOT get this allowlist — their citations remain §11.9 findings unless the forward-pointer carve-out applies. The 4-meta-doc set is BOUNDED; runtime adapter files are not additional law surfaces.
  - **STRUCTURAL-POINTER CARVE-OUT (not law surfaces):** `docs/dev/harness-runtimes.md` and `docs/dev/codebase-memory.md` MAY name/link the 4 meta-docs (and thin adapters such as `CLAUDE.md`) as navigation maps for harness pins / MCP usage. They do **not** prescribe predicates, do **not** expand the meta-doc law set, and must not restate Critical Reminders as a second condensed body.

- **11.10** Docs describe what IS, not what isn't. See §11.37 for the canonical predicate (including the live-design-rationale carve-out). This row is the documentation-parity surface for the broader "describe what isn't" anti-pattern; walk §11.37 for evidence.

- **11.11** Does any doc misframe shared infrastructure as scope-limited ("BaseHandler is for CQRS handlers", "D2Result is for ...")? Frame broadly or list multiple consumers (CQRS handlers, repo handlers, messaging consumers, scheduled jobs, anything handler-shaped).
  - Evidence: scan summary lines on shared infra types.

- **11.12** Does every project / module have a `README.md` (`private/services/{service}/README.md`, `public/packages/dotnet/{lib}/README.md`)? When adding new handlers / entities / config options / public APIs → was the relevant README updated?
  - Evidence: per change touching public surface → README edit.

- **11.13** Are user-facing copy strings (toasts, emails, modals) free of brand names? "Your account" not "your {ProductName}". Brand changes shouldn't require translation migrations.
  - Evidence: per user-facing string → brand-name audit.

- **11.14** Summary-accuracy facet of XML-doc quality — consolidated into §11.17 (canonical xmldoc-quality gate); ID retained for citation stability.
  - Evidence: walk §11.17.

### Documentation best practices (style, structure, brevity)

- **11.15** Do per-lib `README.md` files follow the standard structure?
  - **Required sections**: (1) Title + one-line purpose — what problem this lib solves; (2) Public API — primary types / methods, what they do + when to call; (3) Configuration / Options — Options records + defaults + when to override; (4) Dependencies — project refs + external NuGet packages with versions; (5) Usage examples — ≥1 realistic call site; (6) Telemetry — counters / spans / metrics with tag enumerations; (7) Edge cases / gotchas — known limits, failure modes.
  - **Optional sections**: Architecture diagram, Performance notes, Migration notes (when relevant).
  - **CARVE-OUT — source-gen / abstractions / pure-data-shape libs**: libs whose nature makes specific Required sections trivially-empty MAY omit those sections, PROVIDED the README includes a one-sentence inline justification in the omitted section's position. SourceGen libs typically have NO runtime Telemetry / Configuration / Usage examples (compile-time-only); abstractions-only libs typically have NO Usage examples / Telemetry (the impl libs carry those). Justification line is mandatory — e.g. `### Telemetry\n\nN/A — compile-time-only; runtime telemetry surfaces in the consuming lib's README.` Bare omission without it is a §11.15 finding.
  - Evidence: per new/touched lib README → section presence (or carve-out justification for legitimate omissions).

- **11.16** Do per-service `README.md` files include operational sections beyond the per-lib ones?
  - **Additional required**: (1) Run locally — Docker Compose target + env vars needed; (2) Health check / debugging — health endpoint URL, how to inspect logs / DLQ / DB state; (3) External dependencies — DB names, broker queues / exchanges, downstream services.
  - **CARVE-OUT — stub READMEs**: a per-service README whose entire body is the §11.31 canonical `> **Status: NOT IMPLEMENTED — tracked at <link>**` stub block is NOT required to ship the operational sections until the implementation lands. The stub block IS the §11.1-satisfying present-tense doc for the impl-pending state; the full operational sections land in the SAME commit as the impl code (not the stub-creation commit). Stub READMEs still follow the §11.31 canonical form (status line + tracking link).
  - Evidence: per service README → either (a) section presence (impl shipped), or (b) §11.31 canonical stub form (impl pending).

- **11.17** Are XML doc comments on public types / methods complete, accurate, and helpful at the IntelliSense / hover call site? (Canonical xmldoc-quality gate — §11.14 summary-accuracy facet and §20.10 DX-hover facet consolidate here.)
  - **Required**: `<summary>` (what + when-to-call), `<param>` per parameter (purpose + constraints), `<returns>` (what + edge-case shapes), `<exception>` per documented throw (when), `<remarks>` for non-obvious invariants / threading / disposal.
  - **Quality bar**: summaries are ACCURATE (match what the member actually does) and HELPFUL for IntelliSense / hover — explain the WHAT / WHEN-to-call / WHY / EDGE CASES that aren't obvious from the signature — NOT just `<summary>does the thing</summary>`.
  - **Wrap to 100 chars** within doc comments (per §7.14); long summaries get multiple `///` lines.
  - Evidence: per public symbol → XML doc completeness check.

- **11.18** Do markdown docs use consistent style?
  - **Headings**: ATX-style (`#`, `##`, `###`); one H1 per file.
  - **No heading-level skipping**: heading depth increments by at most one (an H2 → H2 or H3, NEVER H4). Skipped levels break the semantic outline (screen readers, TOC generators, autolink anchors rely on monotonic depth). Audit grep: enumerate heading lines via `grep -nE '^#{1,6} '` → assert depth never jumps by 2+.
  - **Tables**: aligned columns (per §7.13). **Links**: relative paths in-repo (`[label](../path/to.md)`), absolute URLs external. **Code fences**: triple-backtick with language tag. **Lists**: `-` unordered, `1.` ordered. **Emphasis**: `**bold**` for strong, `*italic*` sparingly, no ALL-CAPS for emphasis.
  - **Line length**: markdown is NOT subject to §7.14's 100-char limit; keep prose paragraphs under ~120 chars.
  - Evidence: per new/touched doc → style check.

- **11.19** Are docs free of these common anti-patterns?
  - ❌ **Historical narration** ("This used to use X but we switched to Y because...") — describe what IS.
  - ❌ **Comparison to nonexistent alternatives** ("unlike most libraries, this one...") — just describe what THIS does.
  - ❌ **"Why no X" / "Why we don't have Y" sections** — see §11.37 for the canonical predicate covering both the rule + the live-design-rationale carve-out.
  - ❌ **Marketing prose** ("powerful", "robust", "elegant", "modern") — describe capabilities concretely.
  - ❌ **Multi-paragraph filler** in summary sentences — first sentence should hook + summarize.
  - ❌ **Stale code examples** — every example must compile against current `main`.
  - ❌ **Unexplained acronyms** in user-facing docs (DLQ / TLC / 2LC OK in technical docs; clarify on first use in onboarding docs).
  - ❌ **Self-references** ("see this very document below" / "as mentioned above") when a section anchor or link would do.
  - ❌ **AGENTS.md / PHASE\_\*.md / V2.md citations from KEEP docs** (per §11.9).
  - Evidence: scan for each pattern.

- **11.20** "Describe what IS" reinforcement. See §11.37 for the canonical predicate (carve-out for live design-rationale framing belongs there). This row exists for back-compat with prior journals citing §11.20; walk §11.37 for evidence.

- **11.21** Are docs brief? Long docs aren't more rigorous; they're harder to read and easier to drift.
  - **Heuristic**: per-lib README ≤ 300 lines, per-service README ≤ 500 lines, XML doc summary ≤ 5 lines (use `<remarks>` for longer). When more is needed, split into linked sub-docs.
  - **TOC mandate for long docs**: any markdown doc over 300 lines MUST carry a table-of-contents block at the top (under the title + one-line purpose) with section jump-links (`[Section name](#section-anchor)`) for every `##` section AND every `###` subsection the reader is plausibly searching for. Without a TOC, a 600-line README forces scroll-scanning — effectively write-only. AGENTS.md, rules.md, process.md, and the cross-cutting framework docs are the reference shape. Evidence: per touched doc > 300 lines → TOC present + every `##` heading enumerated + jump-links resolve (per §11.23 link integrity).
  - Evidence: line counts on touched docs.

- **11.22** Do all code examples in docs compile / run against the current codebase?
  - **Why**: stale examples are worse than none — they teach the wrong thing and erode trust in all docs.
  - Evidence: per touched code example → compile / mental-run check.

- **11.23** Are link cross-references valid (no broken in-repo links, no broken anchor refs)?
  - Evidence: per touched doc → link audit (file exists, anchor present in target).

- **11.24** Do CHANGELOG entries (when `versionize` runs) accurately reflect the change scope, with conventional-commit-derived categorization?
  - **Note**: don't hand-edit CHANGELOG.md; let `dotnet versionize` generate from conventional commits.
  - Evidence: post-versionize CHANGELOG → matches commit log.

- **11.25** When introducing a NEW concept / pattern, is it explained ONCE in the canonical doc (PATTERNS.md / per-lib README) with the explanation linked from elsewhere — not re-explained in every consumer?
  - **Why**: prevents drift; one source of truth per concept.
  - Evidence: per concept → single-source-of-truth confirmed.

- **11.26** Are README universal claims ("never throws X", "always returns Y", "no Z accepted", "the result is binary") either GREP-VERIFIED at audit time OR qualified at write time with an explicit carve-out reference?
  - **Why**: a README claiming "implementations never throw `ArgumentException` for caller mistakes" rots silently the moment a sibling method introduces a registration-time `InvalidOperationException` carve-out; readers rely on the universal claim and the surprise costs more than qualifying upfront.
  - **Acceptable forms**: (a) "never throws X **for per-call mistakes** — construction / DI registration is a separate lifecycle concern, see [link]"; (b) "always returns Y **except for the documented carve-outs in [link]**"; (c) at audit time, `grep` to confirm zero counter-examples and note the result inline.
  - **Forbidden**: bare unqualified universal claims that don't survive a grep gate.
  - Evidence: per universal claim in a touched README → grep result OR carve-out reference confirmed.

- **11.27** Are README "X% coverage" / "100% lines / 100% branches" prose claims either backed by a coverage tool gate (codecov, coverlet threshold, CI fail-on-drop) OR rephrased qualitatively ("adversarial coverage across every public surface")?
  - **Why**: unverified percentage claims drift the instant a new method ships without a test — reading as marketing prose that erodes trust.
  - **Acceptable forms**: (a) coverage gate wired up + percentage claim + reference to the gate; (b) qualitative "adversarial coverage across every public surface" framing without numbers.
  - Evidence: per coverage claim → gate reference OR qualitative rephrasing.

- **11.28** Are KEEP docs (READMEs, per-lib docs, xmldoc summaries / remarks, source comments) free of forward-looking framing about future / deferred work?
  - **Forbidden tokens / phrasings** (in any KEEP doc outside the allowlisted paths in §14.1): `the future <X> lib` / `module`; `future [<adjective(s)>] <noun>` (any `future [0-3 adjective tokens] <aggregator|lib|module|matcher|middleware|extractor|emitter>`, including hyphenated compounds like `cross-cutting`); `<X> will eventually` / `will likely`; `will live in <X>` / `will live there`; `when <X> ships`; `until <X> is shipped`; `for now`, `for the time being`, `currently this is X` (when the implication is "soon Y"); `not yet`, `pending <X>`; `in a later phase` / `future phase` / `later phase` / `in future phases`; any framing requiring the reader to know a deferred future state to interpret the current text.
  - **Allowed**: present-tense architectural-boundary statements ("X is OUT OF SCOPE for this lib", "responsibility lives in Y" — no temporal verbs); present-tense cross-lib-integration facts ("logs reach OTLP collectors via the MEL pipeline; the OTLP exporter wiring is owned by separate observability infrastructure"); forward-looking framing inside the §14.1-allowlisted paths (`private/docs/v2/`, `docs/dev/deliverables/`, `MEMORY.md`, `CHANGELOG.md`) + `docs/wip/`.
  - **Tracking-doc allowlist (cross-ref §14.1)**: `private/docs/v2/V2.md`, `private/docs/v2/PHASE_*.md`, and any doc explicitly marked a phase / wave tracking doc are EXEMPT — their job IS phase / deferred-work tracking. The §14.1 allowlist (`private/docs/v2/`, `docs/dev/deliverables/`, `MEMORY.md`, `CHANGELOG.md`) + `docs/wip/` is the authoritative scope.
  - Evidence: `grep -rEn 'the future [A-Z]|future(\s+[a-zA-Z][a-zA-Z\.-]*){0,3}\s+(aggregator|lib|module|matcher|middleware|extractor|emitter)\b|will eventually|will likely|will live (in|there)|when [A-Z][a-zA-Z\.]+ ships|until [A-Z][a-zA-Z\.]+ is shipped|for now,|for the time being|not yet [a-z]|pending [A-Z]|in a later phase|in a future phase|later phase|in future phases' <KEEP scope minus allowlist>` returns empty. The `future ... <noun>` clause allows 0-3 adjective tokens between `future` and the noun, `\b`-anchored so `future modules` / `future moduleX` aren't false-positives.
  - **Why**: "the future Y lib's job" rots the moment Y ships (Y is then current; the doc is wrong) and implies a reader who knows about deferred work. §11.19 forbids the symmetric backward case (`This used to use X`); §14.1 covers the explicit phase-token case; §11.28 closes the generic forward-framing gap.
  - **How**: frame cross-lib integration in present tense with explicit responsibility boundaries. Bad: "that's the future DcsvIo.D2.Telemetry lib's job." Good: "OpenTelemetry SDK setup is OUT OF SCOPE for this lib."

- **11.29** When a project file's dependency set changes (`<ProjectReference>` / `<PackageReference>` add/remove in any `.csproj`; `dependencies` / `devDependencies` / `workspace:*` in any `package.json`), is the corresponding parent overview README's Mermaid dep-graph AND descriptive cross-subgraph dep list updated in the SAME change?
  - **Scope**: any per-lib / per-service / per-package project file that participates in a parent overview README's dep graph. `.NET`: parent = `public/packages/dotnet/README.md` (or per-service equivalent) — §9.8 + §11.6 already enforce `<ProjectReference>` edges. `TypeScript`: parent = the shared-TS overview (e.g. `public/packages/typescript/README.md`, or `private/services/web/README.md` for BFF-internal deps). The Mermaid diagram + cross-subgraph prose reflect the change in the same commit as the project-file edit.
  - **Evidence**: per project-file dep-set diff → parent overview README diff in the SAME commit (Mermaid nodes/edges + descriptive dep list both reflect it). Audit grep: `git diff --name-only HEAD~1` on a commit with `.csproj` / `package.json` dep changes → corresponding parent README also in the diff.
  - **Why**: cross-doc dep parity drift is invisible to pre-flight grep — no regex spans `csproj` → Mermaid graph. §11.29 generalizes §9.8 + §11.6 to ALSO cover `<PackageReference>` (NuGet) edits AND the TS-workspace analog. The single-csproj-edit minimality of "I just added one ProjectReference" is exactly the surface where the parent-README check feels out-of-scope and silently slips.
  - **How**: when editing any project file's dep set, update the parent overview README's Mermaid graph + descriptive dep list in the SAME commit. If no parent README exists for the workspace, propose creating one in the deliverable's distillation rather than letting the gap persist.

- **11.30** Are constant catalogs that meet ANY of these criteria spec-driven via codegen, NOT hand-mirrored?
  1. **Hand-mirrored across languages** — a catalog exists in BOTH `DcsvIo.D2.X` AND `@dcsv-io/d2-x` (or any pair of language packages consuming the same wire value set). Single source MUST be a spec; codegen emits both.
  2. **Wire-protocol contract** — any header / claim / status code / error code / message field / topic name / encoding token crossing the network. Single source MUST be a spec; codegen emits language-specific constants.
  3. **Dual-binding within one language** — e.g. HTTP middleware writes a slot key that a gRPC interceptor reads; both bindings must use the same string. Single source MUST be a spec; codegen emits to both binding csprojs.
  - **Forbidden**: two parallel `.cs` / `.ts` files defining the same constant set kept in sync by convention or a parity test (both DELETED, regenerated from one spec); a `nameof()` / hand-typed string used as a wire value without a spec; a catalog in one language with NO equivalent in the other when both consume the same wire format; an emitter-side closed list that hand-mirrors part of the catalog the spec would own.
  - **Evidence**: per new constant catalog in scope → spec file path + codegen runner cited; per cross-language consumption → both languages consume from same spec path. Pre-flight grep: `find <scope> -name "*.cs" -o -name "*.ts" | xargs grep -l 'public const string\|export const.*= {' | xargs grep -L '\.g\.cs\|\.g\.ts'` → justify each hit (test fixture, domain enum, non-wire constant) or migrate to spec. Manual reading required per §24.13.2 (the regex catches catalog-shaped constants but NOT closed-list smells nested inside emitter source).
  - **Why**: cross-language constant drift is invisible to compile-time checks and to most parity tests (a `TS.X === .NET.X` test catches missing entries, not subtle value differences). Spec-driven codegen makes drift impossible: ONE source, both emitted, every consumer reads the same value. Same logic for dual-binding + wire-protocol contracts.
  - **How**: author the spec under `public/contracts/<topic>/` (framework) or `private/contracts/<topic>/` (product); author per-language emitters (`public/tools/ts-codegen/src/<topic>-emit.ts`, `public/packages/dotnet/<topic>-source-gen/`); commit `.g.ts` + `.g.cs`; add per-VALUE pin tests (§1.18). The 0006 spec-driven catalog migration (`public/contracts/headers/`, `public/contracts/jwt-claims/`, `public/contracts/in-process-keys/` + the four `DcsvIo.D2.Headers.*` catalogs) is the reference shape.

- **11.30.1** When ≥2 producers (across runtimes OR transports within a runtime) emit the SAME wire format (the same RFC 7807 ProblemDetails JSON, gRPC response envelope, AMQP frame), does EVERY field ANY producer writes get written by ALL producers — so a consumer receives the same field-set regardless of origin?
  - **Boundary vs §11.30 / §26.3**: §11.30 governs the spec-driven CONSTANT SET (field NAMES emitted from one spec). §26.3 governs two EMITTERS producing equivalent OUTPUT from a spec. §11.30.1 governs runtime PRODUCERS at emit time — the code that POPULATES the wire payload. A field can be spec-declared (§11.30 ✓) and both generators emit the constant (§26.3 ✓) yet one runtime producer forgets to SET it (§11.30.1 ✗).
  - **Evidence**: per shared wire format with ≥2 producers → enumerate the producers (grep the serializer / builder per runtime + transport) + assert each writes an identical field-set; a per-producer emission test pins each producer's field-set against the shared catalog. A producer omitting a field a sibling writes = FINDING. PARITY.md's wire-format rows enumerate the producers per format.
  - **Why**: spec-driven constants + emitter parity pin the field NAMES and generated constants but neither pins that every runtime producer POPULATES every field. (0017 final review — see Provenance.)
  - **How**: adding a field to a shared wire format updates EVERY producer in the SAME change + ships a per-producer emission test. At PLAN time, any deliverable touching a shared wire format enumerates ALL producers (across runtimes + transports).
  - *Provenance: deliverable 0017 final review (BFF ProblemDetails builder dropping the category extension both .NET producers emitted).*

- **11.31** Are substantive technical claims about runtime behavior + functionality-described-as-shipped either (a) verifiable by reading the cited implementation, (b) stubbed with an explicit `Status: NOT IMPLEMENTED` block when the functionality doesn't exist yet, or (c) rephrased qualitatively to remove the unverifiable specificity?
  - **Scope (a) — substantive technical claims**: any prose claim about runtime behavior — transport ("calls reach the service over gRPC"), infrastructure ("backed by Redis SET NX"), encryption ("payloads are AES-GCM encrypted before publish"), validation ("input is validated via the smart-constructor pattern before any I/O"), control flow ("returns `ServiceUnavailable` if the broker is unreachable"). The cited `file.cs:NN` MUST exhibit the claimed behavior — symbol existence is NOT sufficient (a `EncryptPayload` that no-ops doesn't satisfy "payloads are encrypted").
  - **Scope (b) — functionality described as shipped**: any section reading in present tense as if the functionality exists today. If it is NOT in the codebase, the section MUST open with `> **Status: NOT IMPLEMENTED — tracked at <link>**` (link = `private/docs/v2/`, deliverable plan, tracked issue). Implicit-present-tense framing for unbuilt code is forbidden.
  - **Acceptable forms**: (i) claim + `file.cs:NN` whose code IS the claim; (ii) explicit `Status: NOT IMPLEMENTED` stub block + tracking link; (iii) qualitative rephrasing removing the specific claim.
  - **Forbidden**: (i) present-tense prose describing libs / handlers / endpoints that don't exist; (ii) substantive claims citing symbol existence but not behavior; (iii) "Status: TODO" / "planned" / "Coming soon" framings (those are §11.28 violations — the only acceptable stub framing is `Status: NOT IMPLEMENTED — tracked at <link>`).
  - **Evidence**: per substantive claim in a touched KEEP doc → citation to impl `file.cs:NN` + auditor MUST READ the cited code (§24.13.2) to confirm semantic match. Per "functionality described as shipped" section → codebase ACTUALLY exhibits it (file:line) OR the section opens with the stub block.
  - **Why**: generalizes §11.26 (universal claims grep-verifiable) + §11.27 (coverage % backed by gate) to the substantive-claim + state-honesty class. Empirical: a prior operational-guarantees doc described rate-limiting / idempotency / request-enrichment as shipped when none of those libraries existed — readers and future agents were misled into relying on guarantees that weren't there. Symbol existence ≠ semantic match.

- **11.32** Where intentional duplication of content exists between KEEP docs (e.g. AGENTS.md §5 "Critical Reminders" condensing rules.md §5 predicates), does each duplicated section carry an explicit duplication annotation + cross-pointer to the canonical full version?
  - **Required annotation elements** (form-agnostic): every annotation satisfies four elements — (a) identify the canonical source (file + section), (b) link to it (proper markdown link), (c) state the duplication is by design ("Duplicate of" / "Duplicated from"), (d) name the lockstep-update requirement (cite §11.32 OR "update both in lockstep").
  - **Acceptable forms** (both satisfy the four elements):
    - **Terse footer (default)** — single-line italic adjacent to the content: `_Duplicate of [<short-label>](<full-path-with-anchor>) — keep in lockstep per §11.32._` For routine duplications where the section's framing already signals what it is.
    - **Verbose annotation** — blockquote with full framing: `> **Duplicated from <canonical-doc> §<section> for at-a-glance reference. The canonical full version ... lives at [<link>](<link>) — update both in lockstep.**` For high-priority sections where the relationship is non-obvious or the stakes warrant emphasis.
  - **Forbidden**: bare duplication between KEEP docs WITHOUT the annotation — an accidental two-sources-of-truth situation failing §11.25. Un-annotated cross-doc matches MUST be deduplicated (one canonical home, every other reference a link).
  - **Bounded duplication only**: even annotated duplication is a condensed / summarized form of the canonical, NOT a full copy. If both copies are full-length, deduplicate.
  - **Evidence**: per content match found via cross-doc grep → either (a) annotation present + cross-pointer resolves + condensed-vs-full relationship honest, OR (b) deduplicate to one canonical home + link. Audit grep: pick 5+ headings / phrases appearing in multiple KEEP docs → verify each carries the §11.32 annotation OR is deduplicated.
  - **Why**: bare duplication is where two docs say similar things, an author updates one and forgets the other, and they silently disagree thereafter. §11.25 forbids accidental duplication; §11.32 recognizes legitimate duplication (AGENTS.md's condensed views) and keeps it from drifting via the annotation + lockstep mandate.
  - **Canonical legitimate-duplication sites** (annotation form = identify canonical, link, name lockstep mandate): AGENTS.md §5 "Critical Reminders" ← rules.md §1-§23; AGENTS.md §6 "C# Naming" table ← rules.md §7.1; AGENTS.md MANDATORY block 0 ← process.md §3; MANDATORY block 2 ← rules.md §24.0; MANDATORY block 3 ← the rules.md completeness checklist; PATTERNS.md directory entries ← per-lib READMEs; `.github/copilot-instructions.md` ← rules.md + AGENTS.md.

- **11.33** Is every `.md` file in the repo (excluding the §11 KEEP-doc allowlist exclusions: `docs/wip/`, `docs/archive/`, `docs/dev/deliverables/`, `MEMORY.md`, `CHANGELOG.md`, `private/docs/v2/`) reachable from a root-level entry README via a chain of markdown links?
  - **Reachability roots**: build the graph from `README.md`, `AGENTS.md`, `CONTRIBUTING.md`. Walk every linked `.md` recursively (following only `[label](relative/path.md)` links). Assert every in-scope `.md` is visited ≥1.
  - **Forbidden**: orphan `.md` files with no inbound link from a higher-level README — effectively invisible (the reader must already know the file exists), defeating the purpose of a README.
  - **Allowed exceptions**: (a) READMEs intentionally external to the doc graph (rare, justified inline); (b) the allowlisted `docs/wip/` / `docs/archive/` / `private/docs/v2/` / `docs/dev/deliverables/` / `MEMORY.md` / `CHANGELOG.md` paths.
  - **Evidence**: per audit round, build the reachability graph from the three roots, walk all `[*](*.md)` links recursively, diff the visited set against the in-scope `.md` set (`find . -name '*.md' -not -path './docs/wip/*' -not -path './docs/archive/*' -not -path './docs/dev/deliverables/*' -not -path './private/docs/v2/*' -not -name MEMORY.md -not -name CHANGELOG.md`). The unvisited remainder = orphans = findings.
  - **Why**: discoverability is foundational. New contributors and AI agents find docs by following links from `README.md` / `AGENTS.md` outward. Orphan READMEs accumulate when authors create per-service / per-lib READMEs without updating the parent's link list — silent because no compile / lint check catches it.
  - **How**: authoring or substantially editing a `.md` file adds an inbound link from the parent overview README in the SAME change. Auditing runs the reachability walk + diff; any orphan is fixed in the same audit round (§24.0d).

- **11.34** Does every KEEP doc's opening (Title + first 5 lines) make EXPLICIT both (a) WHO the doc is for + (b) WHAT problem it solves / question it answers?
  - **WHO** = the named consumer / audience (e.g. "operators running this service in production", "module authors integrating with this lib", "consumers of the `IFooHandler` interface"). The audience must be NAMED — not implied by location or title alone.
  - **WHAT** = the specific problem solved / question answered (e.g. "how to debug a failed Geo migration", "the operational runbook for KeyCustodian compromise scenarios"). Must be specific — not generic ("documentation for this lib").
  - **Acceptable opening shape**: `# <title>\n\n<one-sentence WHO + WHAT>\n\n<optional context>`. Example: `# DcsvIo.D2.Caching.Redis\n\nRedis-backed IDistributedCache implementation for module authors integrating distributed caching into their handlers — covers public API, configuration, redaction posture, and known gotchas.`
  - **Forbidden**: "mystery meat" openings that require reading paragraphs to figure out who should care. A package-name title is enough WHAT but doesn't clarify WHO.
  - **Severity**: missing WHO entirely = MEDIUM. Missing BOTH WHO and WHAT = HIGH (the doc is unanchored). Both present but generic = LOW.
  - **Evidence**: per touched KEEP doc → read first 5 lines → assert WHO explicitly identifiable (named consumer) AND WHAT explicitly identifiable (specific problem / question). Implicit-only identification is a finding.

- **11.35** Is the SAME concept named the SAME across KEEP docs?
  - **Scope**: concepts appearing in 2+ KEEP docs MUST use ONE canonical name everywhere. Examples: "scope" not "permission" (per §7.9); "Tier 1 / 2 / 3 cache" (one numbering across PATTERNS.md, per-lib READMEs, source comments); domain terms ("Geopolitical Entity", "WhoIs", "RedactionSpec", "KeyCustodian") carry ONE definition + cross-links.
  - **Required**: (a) define a new domain term ONCE in the canonical doc (per-lib README for lib-scoped, PATTERNS.md / cross-cutting for repo-wide); (b) every subsequent reference uses the canonical term verbatim; (c) every doc introducing the term cross-links to the definition. When two docs disagree, ONE name is canonical (most consistent with §7.9) and the other is updated in the SAME change.
  - **Forbidden**: same concept under different names ("scope" vs "permission"); same name for different concepts ("Tier 1" = L1 cache vs primary locale, undisambiguated); domain-term aliases diverging over time.
  - **Acceptable forms**: (i) one canonical name everywhere; (ii) explicit disambiguation when two similar-sounding terms differ ("Tier 1 cache (L1 in-process)" vs "Tier 1 locale (primary user locale)"); (iii) renaming a term triggers a SAME-commit rename across consumers per §11.1.
  - **Evidence**: per audit round, enumerate concepts in 2+ touched docs (manual reading per §24.13.2 — grep misses synonym drift), verify consistent naming, flag drift. §7.9 covers the code-side rule; §11.35 covers the doc-side mirror.
  - **Why**: terminology drift creates ambiguity — a reader seeing "scope" in one doc and "permission" in another can't tell if they refer to the same thing.

- **11.35.1** When the SAME `Tier N` / `Layer N` nomenclature is used across multiple subsystems with different meanings, is each per-subsystem usage explicitly disambiguated with a qualifier the FIRST time the tier is introduced in each doc?
  - **Scope**: bare "Tier 1/2/3" / "Layer 1/2" across subsystem docs where the SAME ordinals denote DIFFERENT concepts (cache: L1 in-process / L2 distributed / L3 persisted; resilience: registration / call-site / middleware layer; geo data: primary / secondary / tertiary catalog rank).
  - **Required**: the FIRST mention in any doc includes a parenthetical qualifier naming which subsystem's numbering applies (e.g. `Tier 1 (L1 in-process cache)` vs `Tier 1 (registration layer of the resilience pipeline)`). Subsequent references may drop the qualifier once established.
  - **Forbidden**: bare `Tier 1` / `Tier 2` across subsystem docs that look identical to a cross-doc reader.
  - **Sub-predicate of §11.35**: codifies the "same name for different concepts" half of §11.35's forbidden-form clause against the recurring `Tier N` collision class. §11.35's "same concept under different names" half remains separately governed.
  - **Evidence**: per doc using `Tier N` / `Layer N`, first-mention parenthetical present (or the doc deals with only ONE subsystem's numbering). Grep: `grep -nE '\b(Tier|Layer)[ -][0-9]\b' <touched docs>` → per hit, verify disambiguation at first-mention OR single-subsystem scope.

- **11.36** Does every folder-root README that catalogs its child directories use the strict `<lib>/README.md` link form (NOT bare `<lib>/` directory links) AND give a one-line description of each child?
  - **Scope**: any folder with a `README.md` that enumerates sub-folders / significant files (parent overviews such as `public/packages/dotnet/README.md`, `public/packages/typescript/README.md`, `public/tools/README.md`, `private/tools/README.md`, `public/contracts/README.md`, `private/contracts/README.md`).
  - **Required**: every child README listed is linked via its README path (`[label](child-folder/README.md)`) — NOT a bare directory link (`[label](child-folder/)`) — AND carries a one-line description. Bare directory links break the §11.33 reachability graph (the walker follows `[label](path/README.md)` links specifically).
  - **Forbidden**: bare directory links like `[foo/](foo/)` in any parent overview; a non-trivial child README without an inbound link from the parent.
  - **Evidence**: per touched parent overview README → grep for `](.*?/)` bare-dir patterns → expect zero (or per-hit justification). Per non-trivial child README → parent's Contents / Index lists it via `<child-folder>/README.md` link + one-line description.
  - **Why**: a child README technically reachable but practically invisible (parent has `[child/](child/)` with no description) defeats the purpose. (0010-markdown-audit found multiple parent overviews using bare-directory child links, orphaning the children.)

- **11.37** Are `Why no X` / `Why not Y` / `X is intentional absence` section headings absent from KEEP docs?
  - **Scope**: `## Why no X` / `## Why not Y` / `## X is intentionally absent` / `### Intentional absence` / equivalents in any KEEP doc (READMEs, framework docs, source comments, xmldoc, JSON `$note`, generated-code source-of-truth surfaces).
  - **Required**: KEEP docs describe what IS. `Why no X` sections describe a deliberately-absent concept — implying a reader who already knows about it (the v1-retrospective / dead-concept framing §11.10 + §11.19 + §11.20 forbid).
  - **Reframe**: replace `Why no X` with positive scope framings — `Scope: this lib does Y` replaces `Why not X` when X is out of scope. If the rationale for an absent thing is genuinely necessary (a deliberately-rejected industry-standard alternative), it belongs in `private/docs/v2/` decision-records or an explicit `Out of scope` section in a planning doc, NOT a KEEP-doc README.
  - **Forbidden**: any KEEP-doc heading matching `Why no ` / `Why not ` / `Why we don'?t` / `Intentional absence` / similar.
  - **CARVE-OUT — design-rationale FAQ**: §11.37 forbids `Why no X` framings that describe DEAD concepts (never planned; the reader's awareness is purely v1-retrospective). It does NOT forbid design-rationale sections explaining LIVE deliberate choices vs reasonable alternatives a future contributor might propose (`no BaseHandler wrapping` with a ~60 ns vs ~5 µs perf rationale; `custom parser over Roslyn`; `overlays vs hand-editing Tier 1 src-data`). Distinguishing test: does the body explain a deliberate choice about something that DOES exist + would a wrong turn cost real effort? If yes → not the antipattern; the rationale belongs in the README. Per the carve-out, prefer present-tense `Design rationale: <thing>` headings over `Why no X` / `Why not Y` (the former reads forward; the latter pattern-matches the dead-concept surface even when the body is legitimate). `Why X instead of Y` (live trade-off) is permitted but `Design rationale: X over Y` is preferred.
  - **Evidence**: per touched KEEP doc → `grep -rEn '^#{1,6}[[:space:]]*(Why no |Why not |Why we don'\''?t |Intentional absence |No X )' <KEEP scope>` → expect zero (or per-hit reframe to positive scope statement, OR carve-out applicability check + rename to `Design rationale: <thing>`).
  - **Why**: dead-concept framing implies a reader who knows what they're missing; KEEP docs serve readers who don't. Empirical: 0010-markdown-audit found 5 `Why no X` headings implying a v1-aware reader. Carve-out justification: 0011 Step 4 found 4 of those 5 document LIVE choices a contributor might re-propose — deleting the rationale risks wasting the next contributor's effort on the rejected path. Positive scope framings ("Scope: this lib does Y") restore forward-readability; `Design rationale: <thing>` keeps the rationale while shedding the dead-concept surface.
  - **How**: if the reader doesn't need to know about the absent concept → don't write the section. If genuinely dead → `Out of scope` positive framing, or move to `private/docs/v2/` / an ADR. If the choice is LIVE → use the `Design rationale: <thing>` carve-out form.

- **11.38** Stub READMEs MUST follow the §11.31 canonical `Status: NOT IMPLEMENTED` form, not ad-hoc framings.
  - **Scope**: any README for a service / lib / package whose implementation has not yet shipped (`private/services/{stub-service}/README.md`, `public/packages/{dotnet,typescript}/{stub-lib}/README.md`, equivalents).
  - **Required**: the stub opens with `> **Status: NOT IMPLEMENTED — tracked at <link>**` where `<link>` points at a tracking doc (`private/docs/v2/V2.md`, a `private/docs/v2/PHASE_*.md`, a tracked issue, the deliverable in flight). The body MAY include a one-paragraph scope statement; it MUST NOT include implementation-described-as-shipped prose (§11.31).
  - **Forbidden**: `Status: TODO` / `planned` / `coming soon` / `When to expand this README` / `(placeholder)` / "Will be implemented in" framings — §11.28 + §11.31 violations rolled together.
  - **Evidence**: per stub README → presence of the canonical `> **Status: NOT IMPLEMENTED — tracked at <link>**` block (verbatim, not a paraphrase).
  - **Why**: `Status: TODO` / `coming soon` is a forward-framing violation (§11.28) AND a §11.31 functionality-described-as-shipped violation. (0010-markdown-audit found 8 stub READMEs using inconsistent framings.)
  - **How**: use the canonical form verbatim; when the lib ships, the stub block is replaced by the full §11.15 / §11.16-required structure in the same change as the impl. Cross-ref §11.16 carve-out (stub READMEs are exempt from per-service operational sections until impl ships).

- **11.39** When updating a markdown reference to a path that moved, are ALL THREE forms fixed in the SAME edit — (a) the link TARGET inside `](path)`, (b) the backtick DISPLAY LABEL `` [`path`] `` (or any inline `` `path` `` code-span naming the old location), AND (c) non-rendering comment text (Mermaid `%%`, HTML `<!-- -->`)?
  - **Scope**: any KEEP doc where a moved file / folder path is referenced — README link tables, cross-doc references, dep-graph diagrams, file-layout tables, any `` `path` `` code-span naming a location. Fires whenever a deliverable moves a referenced file.
  - **Required**: a single fix edits the target, the display label, AND any comment occurrence in lockstep. Fixing only the target leaves the rendered label lying about the location.
  - **Forbidden**: fixing only the `](…)` target and leaving a stale backtick display label or diagram comment — link-resolution checkers pass on a correct target while the rendered text still names the old location.
  - **Evidence**: per moved-path token → grep it as BOTH a `](…)` target AND a `` [`…`] `` / `` `…` `` display label AND `%%` / `<!--` comment text across touched docs (e.g. `grep -rn 'auth-abstractions' docs/ server/` returning zero stale occurrences in ALL three forms). Any stale display label / comment surviving a target-only fix = FINDING-MEDIUM.
  - **Why**: a moved-path reference has up to three independent textual occurrences and link-resolution tooling validates only one. (0010 shared-folder reorg: a Fixer corrected the `](…)` target but left the backtick label + a comment naming the old path — re-flagged next sweep as a §11.3 finding.)
  - **How**: grep the old path token across the doc set, classify each hit as target / label / comment, fix all three in one edit. Pair with §11.3 (label must resolve to the moved location) + §11.23. Applies to every deliverable that moves a referenced file. Cross-ref §24.13.3c (Fixer mechanical sister-sweep).

- **11.40** Do code examples in KEEP docs use the correct parameter order + names as declared in the current source? Do registry lists and feature catalogs enumerate EVERY overload, receiver, and variant — not a partial subset? Does a claimed "inspect 0" / "build clean" gate certificate reflect a FULL-SOLUTION run, not a subset?
  - **Evidence**: per code example in a touched KEEP doc → (a) grep / LSP the cited method signature, (b) confirm the example's argument order + parameter names match; any inversion or stale name = FINDING-MEDIUM. Per registry / feature catalog → enumerate cataloged items vs every overload / receiver in source (`Grep` the builder / extension class for `public` method count); any omitted overload / receiver = FINDING-MEDIUM. Per "inspect 0 warnings" / "build clean" claim → confirm the command was `jb inspectcode D2.slnx` (full solution) OR `dotnet build D2.slnx`, NOT a per-lib / per-file subset; a per-file subset yielding 0 while the integrated run emits warnings = FINDING-HIGH (false gate certificate). Cross-ref §24.21.
  - **Why**: doc code-examples drift from real signatures + registries under-enumerate overloads, and only an integrated full-solution build catches the compile-shaped ones. (0015 final-review: an inverted-arg `CreateD2Index` example, a registry listing 3 of 4 anonymize-receiver families, a `[JsonConverter]` missing `typeof()`, and a per-file "inspect 0" hiding 2 full-solution warnings.)
  - **How**: (1) look up the referenced method's signature via Grep / LSP and verify argument order + names; (2) count overloads / receivers by grepping the builder class for `public` + the method family; (3) record the literal full-solution command in the journal. Cross-ref §11.22, §11.3, §24.21.

- **11.41** When reconciling KEEP or tracking docs to a model / decision change, is the named focus-section list treated as the EDITING starting point ONLY — with the reconciliation audit sweeping the ENTIRE document (and the whole doc-set) for the superseded model, not just the named focus sections?
  - **Scope**: any deliverable reconciling docs to a model / decision change (a pivot, ADR amendment, renamed convention, superseded pattern). The plan names a focus-section list (sections KNOWN to describe the old model); that list scopes where editing STARTS, NOT where the audit LOOKS. A finding is any old-model statement ANYWHERE in a touched doc.
  - **Required**: the Implementer brief names the focus sections (efficient editing entry points); the AUDIT brief mandates a whole-doc sweep — grep the old-model vocabulary across the entire document + doc-set, then read each hit — NOT scoped to the focus list.
  - **Forbidden**: an audit that walks only the focus-section list and declares the doc reconciled — leaving the old model live in unlisted sections (silent in-doc contradictions).
  - **Evidence**: per reconciled doc → the audit's grep of the old-model vocabulary spans the WHOLE doc (`grep -nE '<old-model terms>' <full doc path>`, not a line-range subset) + every hit read + classified; the journal records the whole-doc grep command. Any old-model statement surviving in a non-focus section = FINDING.
  - **Why**: a focus-list scopes edits efficiently but used AS the audit scope leaves the old model live in unlisted sections. (0021 Step-4 — see Provenance.)
  - **How**: at PLAN time, name the focus sections for the Implementer + enumerate the old-model vocabulary tokens for the audit. The Auditor greps the vocabulary across the entire doc + doc-set, reads every hit, classifies it (cross-ref §11.42) + §24.13.
  - *Provenance: deliverable 0021 (the auth mint-once-forward pivot — Step-4's 7 stale pockets all sat outside the named focus sections).*

- **11.42** A change that supersedes part of a system leaves SURVIVORS — unchanged subsystems whose docs still carry surface mentions of the old model. Before reconciling, is an explicit Survivors list enumerated, with each old-model mention classified as "reframe the stale framing" vs "survivor — preserve"?
  - **Scope**: any model / decision-change reconciliation (same surface as §11.41). Adjacent subsystems survive unchanged but their docs reference the old model on the surface — those mentions LOOK stale while the subsystem is unchanged. Gutting a survivor while reframing genuinely-stale framing is the over-reconciliation failure mode.
  - **Required**: the plan maintains an explicit Survivors list; every reframe brief carries it + a per-mention "describes-old? Y/N" classification. Each mention is classified "reframe the stale framing" (subsystem changed; framing now wrong) or "survivor — preserve" (subsystem unchanged; mention still accurate).
  - **Forbidden**: a reconciliation that sweeps the old-model vocabulary and reframes / deletes every hit without the survivor classification — that guts working content. A survivor's surface mention is NOT a finding; reframing it INTO inaccuracy IS.
  - **Evidence**: the Survivors list + per-mention "describes-old? Y/N" classification in the plan; per old-model mention → either reframed (subsystem changed) or preserved-as-survivor (subsystem unchanged). Gutting a survivor = FINDING.
  - **Why**: a survivor's surface mentions look stale but the subsystem is unchanged; over-reconciling guts working content. (0021 — see Provenance.)
  - **How**: at PLAN time, enumerate the Survivors alongside the focus-section list (§11.41). Every reframe brief carries the Survivors list + reframe-vs-survivor classification; the Auditor flags both un-reframed stale framing AND gutted survivors. Cross-ref §11.41 (the sweep FINDS the mentions; this predicate CLASSIFIES them).
  - *Provenance: deliverable 0021 (the `act`-chain / BFF→Edge token / `x-d2-context` / anon-mint survivors; the `auth/abstractions` act-chain over-reconciliation trap).*

- **11.43** Does the doc set follow the fixed tier hierarchy, with each tier pointing to the tier below rather than restating it — and are ephemeral holding-pens pruned once their work ships?
  - **Tier structure** (persistent): Tier 1 — `private/docs/v2/V2.md` (whole-project phase map + one-line status per phase + vision); Tier 2 — `private/docs/v2/PHASE_N.md` (per-phase deliverable DAG + per-deliverable scope/status/deps + build order); Tier 3 — `docs/dev/deliverables/NNNN.md` + ADRs (per-deliverable ship doc + decisions + lessons); Reference — KEEP docs (`PATTERNS`, `rules`, `process`, `TESTS`, …) + per-lib/service READMEs (current-truth API and conventions).
  - **Ephemeral holding-pens** (fold in on ship, then pruned): research docs, design annexes (`private/docs/v2/PHASE_N_<concern>.md`), `docs/wip/NNNN/` working journals. Once the deliverable ships, the durable form is the ship doc + ADRs; the holding-pen is pruned.
  - **The pointer rule**: each tier points to the tier below — it does not restate what that tier owns. No two docs are a redundant source of truth for the same status or plan.
  - **Evidence**: per new/modified doc → lands at its correct tier; per shipped deliverable → holding-pen pruned or tracked-to-prune; per cross-tier reference → a pointer (link), not a restatement. Full tier table: `docs/README.md` "How the docs are organized."
  - **Why**: redundant sources of truth drift independently — a tracking doc and two design annexes each claiming a different status corrupts progress tracking. The tier model makes altitude unambiguous; pruning on ship removes the stale artifact before it misleads.

- **11.44** Do docs, ADRs, and code comments use plain, direct language — cutting adjectives and metaphors that say less than their literal rewrite?
  - **Forbidden forms**: phrases where the literal rewrite carries more information — "load-bearing properties" (say "required properties" or name them), "by physics" (state the actual constraint), "battle-tested" (cite the evidence), "non-trivially" / "inherently" / "fundamentally" (adverbs that add nothing).
  - **Evidence**: per touched KEEP doc → spot-check for adjectives / metaphors whose removal would make the sentence more precise; flag any sentence where the reader must decode a metaphor to understand the literal meaning.
  - **Why**: flourishes obscure meaning and read as filler — a reader who doesn't know the codebase can't decode "load-bearing" without already knowing what it protects; the literal statement is shorter and clearer.
  - **How**: prefer the literal statement. Before publishing, ask "does the adjective / metaphor add information, or just rhythm?" If rhythm only, cut it.

- **11.45** Are agent-facing docs (AGENTS.md, the rules catalog, process.md, the reference docs agents read per dispatch / audit round) written agent-first — human navigability SECONDARY, never bought with context bloat?
  - **Required**: the densest faithful form wins — plain-text flows (indented lines + `→` arrows) over Mermaid / box-drawing art (both FORBIDDEN in agent-facing docs); one canonical home per passage with pointers elsewhere (§11.25 / §11.32), never a re-explanation.
  - **Forbidden**: any human-only nicety that costs agent tokens (decorative diagram, ASCII / line art, restatement-for-readability) on the agent read path — a defect, not a feature.
  - **CARVE-OUT**: tables where the table IS the law (naming tables §7.1, cluster / partition maps, the doc-update map) carry content, not decoration → keep. Rendered-for-humans docs OFF the agent read path (user-facing copy, marketing) are out of scope.
  - **Evidence**: per touched agent-facing doc → grep for Mermaid code fences + line-art glyphs (Unicode U+2500–U+257F) → expect zero; per human-only formatting element → justify it carries content (a law-bearing table) or cut it.
  - **Why**: AGENTS.md is injected into every dispatch + rule categories are read per audit round — every byte of human-only nicety is paid on every sub-agent, every round; rendered diagrams buy agents nothing and alignment-sensitive art wraps badly.

- **11.46** **Docs dual-home.** Are framework ADRs under `public/docs/adrs/` with a **Visibility: PUBLIC** banner on every file? Are product/host ADRs under `private/docs/adrs/`? Does process / agent law stay under monorepo-root `docs/dev/`? Do monorepo KEEP reference defaults (`COMMANDS`, `PATTERNS`, `TESTS`, …) remain private root `docs/`? Do public docs avoid citing non-export operator paths as live SoT / clone requirements?
  - **Evidence:** public ADR banners 100%; no live root `docs/adrs/` SoT; public README/ADRs free of `private/**` operator steps as required clone paths.
  - **Why:** dual docs homes keep OSS export free of product runbooks while private monorepo KEEP stays complete for operators.
  - **How:** place new framework ADRs under public; product ADRs under private; process under `docs/dev/`.

- **11.47** **Brand surfaces.** Do public KEEP docs and public package surfaces use the **D2** framework brand (not product SaaS narrative)? Do private product tracking/docs use **D2-WORX** / product hosts where appropriate? Do public package IDs never contain `worx`?
  - **Evidence:** residual greps `D2-WORX|D²-WORX` under `public/` (excl. `coverage/**`, `node_modules/**`, `**/Generated/**`, bin/obj/dist) → 0 outside narrow historical allowlist (e.g. `/old/v1/D2-WORX/` path cites); public package ids `DcsvIo.D2.*` / `@dcsv-io/d2-*`.
  - **Why:** product brand on the export surface confuses OSS consumers and implies closed SaaS requirements for open libs.
  - **How:** reframe public prose to D2; keep product brand on private monorepo docs; never mint `worx` package ids on the open surface.

<sup>[↑ jump to top](#top)</sup>

---

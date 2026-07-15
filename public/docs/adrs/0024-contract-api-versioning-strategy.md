<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0024: Contract & API versioning strategy — additive-by-default behind one always-on strict breaking gate with a per-break force valve, two version handles (wire generation in the name, release in the semver), and removal on telemetry rather than on a date

- **Status**: Accepted
- **Date**: 2026-06-22
- **Deliverable**: Edge contract-versioning groundwork

> The decisions here are settled. The only item left open is the external support-window _length_ (a number, not a strategy choice), which is not fixed because committing to a duration without a product to support would be guesswork; see [Out of current scope](#out-of-current-scope).

## Context

D2 authors every operation once in TypeSpec and emits its representations from there ([ADR-0021](0021-unified-operation-contract-idl.md)): the gRPC `.proto` messages and services, the per-version OpenAPI document, the JSON `*.spec.json` constant catalogs the source-gen pipelines consume, the REST route registrations, the in-process leaf interface, and the i18n message keys. Forty-one `*.spec.json` catalogs, four shared `.proto` files, ten i18n locale files, and a growing TypeSpec surface already live under `public/contracts/`. The contract surface is small today and will balloon as the Edge and its downstream services ship — every new operation, every new error code, every new scope adds to the wire surface that consumers depend on.

The four shared `.proto` files (`public/contracts/protos/common/v1/*`) are currently hand-authored at `d2.common.v1` — a transitional leftover predating the TypeSpec-first pipeline. The direction is to bring them under the TypeSpec/emitter pipeline (single source of truth, per [ADR-0021](0021-unified-operation-contract-idl.md)), emit them at a buf-idiomatic `d2/common/vN` path at the correct generation, and remove the `buf` `PACKAGE_DIRECTORY_MATCH` lint exception that accommodates the current `common/v1` path. See [Shared/common wire types](#sharedcommon-wire-types) for the versioning policy, and the proto-unification follow-on tracked in the roadmap for execution details.

Today **every consumer is in-repo**. A breaking change to a contract and the update to every consumer of that contract land in a single pull request and deploy together; no external party holds a pinned version, and there is no deploy window during which an old client talks to a new server. That property will not hold forever. Three forces erode it on a known trajectory: external-facing APIs (a public REST surface, partner integrations) where the consumer is outside the repo and outside the deploy; multiple teams evolving contracts independently, where one team's field-number reuse silently corrupts another team's payloads; and the eventual open-sourcing of the shared fundamentals (`DcsvIo.D2.*`, the `@dcsv-io/d2-*` packages), where external adopters pin a published version and expect a stability contract. A versioning strategy has to be **lean for the in-repo-atomic-deploy reality of today** while **scaling without re-architecture to the external-pinning reality of tomorrow**.

The existing versioning policy is documented narratively as a section of the v2 architecture plan. It establishes several durable rules this ADR keeps: version from day one, one source of truth per concern, conventional-commit-driven product version bumps via `versionize`, a single product semver triple in the private monorepo version anchor (illustration — not required for public clone) rather than only per-package versions, proto directory-and-package `vN` versioning, `Asp.Versioning.Http` URL-path versioning for REST, central package management, generator-driven database migrations, and a breaking-change catalog (field removed/renamed/retyped, RPC or endpoint removed, error code changed, required header changed). It also takes one stance this ADR **revisits**: it **defers** the proto `reserved` keyword on the rationale that single-team, pre-production, greenfield work derives "zero value vs real line noise" from it. That deferral was sound under hand-authored proto where a human chooses field numbers; it is **not** sound under the realities below, and reversing it is one of this ADR's central changes.

**The contract generation count starts at `v2`, not `v1`.** A frozen earlier codebase lives read-only at `/old/v1/D2-WORX/`. It was the first generation of this same product — an alpha-grade build of these same plans, implemented differently — so the contract/wire generation count **continues at `v2`** rather than restarting at a fresh `v1`. Throughout this ADR, "`v2`" names the **wire/contract generation**, which happens to line up with the project being on its second iteration. The committed protos carry `d2.keycustodian.v2alpha` following the renumber that adopted this strategy (see [Stability channels](#stability-channels--start-in-v2alpha-climb-to-stable-v2) and the [implementation note](#implementation-note--renumber-the-committed-protos)).

Two realities of the current pipeline make the policy a goal rather than an enforced guarantee:

- **Proto field numbers are assigned positionally, and removed numbers are not reserved.** The proto emitter walks each message's fields in declaration order and assigns field numbers `1, 2, 3, …` by position (the emitter assigns "1-based field numbers" in declaration order; a sample emitted file carries `string kid = 1; bytes payload = 2;` with no `reserved` statement anywhere). This has two consequences that turn a proto breaking-change gate into theater. Reordering or inserting a field **renumbers every field after it** — a silent wire break, because field numbers are the wire-format identity in protobuf. And deleting a field **shifts every subsequent field's number down by one** and frees the deleted number for silent reuse — exactly the multi-team collision the `reserved` keyword exists to prevent, and exactly the corruption an old client suffers when a freed number is reassigned to a differently-typed field. Until the emitter (a) honors an explicit per-field number pin and (b) emits `reserved` for removed numbers, a `buf breaking` gate would pass changes that corrupt the wire.

- **A breaking-change gate is not wired.** Nothing in CI today blocks a contributor from making an accidental wire-breaking change. The policy says "when in doubt, bump the version," but a human "when in doubt" is not a gate — it catches deliberate breaks the author already noticed, not the accidental ones the author did not.

The decision below adopts an industry-grounded, layered strategy whose complete machinery is built now and runs behind a single **always-on strict breaking-change gate** from day one. The gate never blocks a deliberate break — a **per-break force valve** lets the author override it consciously — but it always catches the *accidental* one. The now-vs-later difference is not a mode the gate runs in; it is **how often the force valve is pulled**: freely now, while every consumer is in-repo and deploys atomically, and rarely later, once an external consumer pins a version. The strategy scales to external pinning by *behavior change* (stop forcing; start coexisting + deprecating) rather than by re-architecture. It is grounded in established practice rather than invented: Google's API Improvement Proposals AIP-180 (the breaking-change catalog) and AIP-185 (major version in the package path, `alpha`/`beta` stability channels); the protobuf best-practices guidance that field numbers are immutable, that `reserved` guards deletions, that evolution is additive, and that a new major package should be rare; Buf's `buf breaking` checker (its `FILE`/`PACKAGE`/`WIRE`/`WIRE_JSON` rule categories and its `buf skip breaking` label as the intentional-break force-flag, plus its documented opinion that a new `vN` package is preferable to an in-place break); Confluent Schema Registry's compatibility modes (`BACKWARD`/`FORWARD`/`FULL`, with their transitive variants); RFC 8594 (the `Sunset` HTTP header) and RFC 9745 (the `Deprecation` HTTP header); Martin Fowler's Parallel Change (expand → migrate → contract) and Tolerant Reader patterns; Pact consumer-driven contracts with `can-i-deploy`; and the REST-scheme exemplars Stripe (dated, pinned versions with version-change modules), GitHub (a date header), and Shopify (quarterly URL-path versions). TypeSpec's own `@typespec/versioning` library (`@added`/`@removed`/`@renamedFrom`/`@madeOptional`/`@typeChangedFrom`) is the modeling vehicle that fits the IDL-first pipeline already in place.

## Decision

Adopt a **layered contract-evolution model**, ordered by how often each layer is exercised, with **TypeSpec as the single source of truth** for every versioned artifact. **Build the entire system now** — the modeling, the field-number pinning and `reserved` emission, the always-on strict breaking-change gate, the coexistence machinery, and the deprecation-lifecycle machinery. There is **no permissive mode and no global "force-defaulted-on" switch**: the gate is **always strict and always on**, and it blocks a breaking change unless that change is **explicitly forced per-break**. The now-vs-later difference is therefore not what gets built and not a mode the gate runs in — it is the **prevalence of the per-break force valve**: pulled freely today (every break under atomic deploy is consciously forced), pulled rarely once an external consumer pins. The only items that genuinely wait are the two runtime pieces that need the running Edge request pipeline (see [Now-behavior vs later-behavior under one always-strict gate](#now-behavior-vs-later-behavior--under-one-always-strict-gate)).

### The layered model — ordered by frequency of use

1. **Additive-only is the default; a new major is rare.** The overwhelmingly common change is adding a field, a message, an endpoint, or an enum member with a fresh identity that no existing consumer reads. Such a change is non-breaking by construction and ships within the current version. A new major version is the rare exception reserved for a change that cannot be made additively, not the routine response to evolution.

2. **Tolerant Reader on every consumer makes additive changes free.** Every consumer — the .NET clients, the BFF, internal service-to-service callers — ignores fields it does not recognize rather than failing on their presence. This is the Tolerant Reader pattern, and it is what makes layer 1 actually free: a server can add a field and deploy ahead of its consumers without breaking a single one, because no consumer chokes on the unknown. This is a property the generated DTOs and their deserializers must hold, asserted by test, not a convention left to each consumer's discipline.

3. **Expand-and-Contract is how anything changes or is removed.** No field, message, or endpoint is ever mutated or deleted in place while a consumer might still use it. The change is staged as Parallel Change: **expand** (add the new shape alongside the old), **migrate** (move every consumer to the new shape), **contract** (remove the old shape once no consumer reads it). Renames, type changes, and removals are all special cases of this three-step move. This is the mechanism that layer 5's removal step plugs into.

4. **An always-on strict breaking-change gate plus a per-break force valve makes accidental breaks impossible.** A path-filtered, pull-request-blocking gate compares the proposed contract against the baseline branch and **blocks** any change in the breaking catalog — from day one, with no permissive mode. It catches the **accidental** break — the renumbered field, the silently-deleted enum member, the retyped property the author did not notice — and the **per-break force valve** (a PR label, a commit footer, or equivalent) is the only way past it, used **freely now and rarely later**. The gate is what converts "when in doubt, bump" from a human hope into a machine guarantee, and the force valve is what keeps a *deliberate* break possible without weakening the gate for the *accidental* one.

5. **Removal happens on telemetry, never on a date alone.** The contract step of Expand-and-Contract — actually deleting the deprecated shape — is gated on **observed zero usage**, measured from request telemetry, not on a calendar deadline. A deprecated field is removed when the metrics show no consumer has touched it, not when an arbitrary sunset date passes. (For now-behavior under atomic deploy this is immediate-by-construction; for later-behavior it is the telemetry-gated removal the deprecation lifecycle below describes — and it **requires the running Edge request pipeline**, because the telemetry it reads is request telemetry the Edge emits.)

6. **`Deprecation` and `Sunset` response headers plus a written support window signal external consumers.** For any externally-pinned surface, a deprecated operation emits the RFC 9745 `Deprecation` header and the RFC 8594 `Sunset` header, and a written support-window policy states how long a deprecated version is honored. This is how an external consumer — who is not in the repo and cannot be migrated by a pull request — learns that a shape is going away and by when. (The headers are emitted by Edge response middleware, so they apply when the Edge pipeline is present.)

### TypeSpec is the single source of truth for versioning

Version evolution is **modeled in TypeSpec**, not in the emitted artifacts. The `@typespec/versioning` decorators — `@added`, `@removed`, `@renamedFrom`, `@madeOptional`, `@typeChangedFrom` — annotate the production namespaces, and the per-version artifacts (the `vN` proto packages, the per-version OpenAPI documents, the versioned DTOs) are **emitted** from those annotations. No per-version artifact is ever hand-edited; a version difference is authored once on the operation and projected into every representation, exactly as the operation itself is. This keeps versioning inside the single-source-of-truth guarantee the IDL already provides — the C# and TypeScript and proto and OpenAPI views of a version cannot disagree because they derive from one annotated source.

### Stability channels — start in `v2alpha`, climb to stable `v2`

**Everything starts in `v2alpha` and climbs the maturity ladder `v2alpha → v2beta → v2` (stable).** The maturity lives in the **proto package name and the generated wire C# namespace**, following Google AIP-185, and in the **semver pre-release label** — not in the published npm/NuGet package ID:

- proto package: `d2.keycustodian.v2alpha` → `d2.keycustodian.v2beta` → `d2.keycustodian.v2`
- generated wire C# namespace: `D2.Services.Protos.KeyCustodian.V2Alpha` → `…V2Beta` → `…V2`
- published .NET package ID: `D2.KeyCustodian.V2.*` (stable generation in the name; pre-stable rides the semver pre-release label `1.0.0-alpha.N` / `0.x`)
- published TS package ID: `@dcsv-io/d2-keycustodian-v2` (stable generation in the name; pre-stable rides the npm dist-tag + `0.x` semver)

The proto package and the generated wire C# namespace carry the stability channel per AIP-185 — that is appropriate, because they are wire-identity artifacts and their names are not pinnable npm/NuGet identifiers. The published package ID carries only the **stable wire generation** (`V2`, `v2`) so that side-by-side coexistence of stable breaking generations is possible by name. A pre-stable surface is signaled to consumers via the semver pre-release label (`0.x` / `1.0.0-alpha.N`) and the npm dist-tag or NuGet pre-release flag — not by a separate package ID.

**Pre-stable channels (`v2alpha`, `v2beta`) are gate-exempt and sit at `0.x` semver.** A surface in a pre-stable channel may **break freely** while it hardens — no force valve is pulled, because its instability is declared in its name and no consumer is entitled to rely on it. `0.x` is semver's own "anything may change" range, so it is the honest "no compatibility promise yet" signal. The version-plus-changelog habit still runs in pre-stable: a break in `0.x` bumps the **MINOR** (e.g. `0.4.0 → 0.5.0`) and writes a changelog entry, so the muscle memory and the institutional record build from the start.

**Graduation = promoting (renaming) to the next channel.** `v2alpha → v2beta → v2` is a rename allowed **without a force-break**, because a pre-stable surface carries no compatibility promise. In-repo consumers migrate atomically at the promotion. At graduation to stable **`v2`**, the package cuts **`1.0.0`** as its first stable release.

**Stable `v2` is gate-enforced.** Once a surface graduates to `v2`, the breaking-change gate enforces against it: a break of stable `v2` is **forced in place now** (pre-external, atomic deploy) or **mints a new named generation `v3`** later (the strict, external path — see [Two version handles](#two-version-handles--wire-generation-in-the-name-release-in-the-semver)).

**The payoff:** because everything starts gate-exempt in `v2alpha`, the per-break force valve only ever fires once a surface has **graduated to stable `v2`**. The "force only fires on surfaces you've declared stable" property is achieved **structurally** — by where a surface sits on the ladder — rather than by discipline. Adopting channels now, while the system is pre-alpha and every surface is in flux, is nearly free; retrofitting a stability-channel distinction after surfaces have shipped as bare stable is costly, because consumers would already treat a bare stable name as a compatibility promise.

### Shared/common wire types

**Hand-maintained protos are transitional.** TypeSpec is the single source of truth for every operation contract ([ADR-0021](0021-unified-operation-contract-idl.md)); hand-maintained `.proto` files are a transitional state, not the end state. The four shared `public/contracts/protos/common/v1/*` files are the current exception: they predate the TypeSpec pipeline and remain hand-authored at `d2.common.v1` until the proto-unification follow-on brings them under the emitter (emitting at the buf-idiomatic `d2/common/vN` path and removing the `buf` `PACKAGE_DIRECTORY_MATCH` lint exception that accommodates the current layout). At that point, no proto in the repo is hand-maintained.

**Versioning policy for shared/common wire types.** Shared types (the `D2Result` envelope, health, and similar foundational wire shapes) are **born stable** — they are foundational infrastructure, not an experimental service surface, so they do not climb the `alpha → beta → stable` ladder that service-specific surfaces do. They version **independently** via per-package semver, decoupled from any service generation. The current `d2.common.v1` gated as stable is exactly this policy in effect. The `common/v1 → d2/common/vN` renumber and the question of which generation number to target ride the proto-unification follow-on, where the correct generation for the unified path is determined alongside the emitter work.

### Field-number ownership = author-pinned + `reserved` emission

The emitter is enhanced **now** to stop assigning field numbers positionally. Field numbers are **author-pinned**: a `@field(n)`-style decorator on the TypeSpec model property — named `@d2Field(n)` in the existing `@d2*` decorator vocabulary, or `@field` reused from `@typespec/protobuf`; either way a **to-be-built convention this ADR adopts** — plus an **explicitly authored reserved list**. The `.tsp` therefore owns the field numbers and the `reserved` set directly, the emitter **(a) honors the pinned number** so a field's wire number is stable across reorders and insertions and **(b) emits `reserved` statements for removed field numbers** (and reserved names where a name must never be reused) so a deleted number can never be silently reassigned. Author-pinning is the decided mechanism — it is deterministic, keeps the `.tsp` as the single source, produces a reviewable diff the instant a number changes, and mirrors protobuf's own discipline of a human owning field numbers. (An emitter-managed registry that auto-assigns and auto-reserves was weighed and set aside: it is more ergonomic but introduces a stateful build artifact that must itself be committed, reviewed, and kept consistent.)

The pins and the gate are **real and enforcing from day one**, not theater. The gate blocks a change to a pinned number; the **per-break force valve** is what permits *breaking a pin in place* — and, because a forced break under atomic deploy deletes the old definition outright, the force valve is also what permits **skipping `reserved` on that break** (there is no surviving consumer to reserve against). Once the force valve is retired (the first external pin or open-sourced lib), `reserved` **accumulates on every delete** and pinned numbers are honored as immutable, because a coexisting old major now genuinely depends on them.

This **reverses the prior deferral of the `reserved` keyword** by building the capability now: the deferral assumed a human picking field numbers in a single-team greenfield, where `reserved` is line noise; under positional auto-assignment, multi-team evolution, and an eventual external surface, `reserved` is the guard that keeps the wire honest. Building it now — rather than retrofitting it when the first external consumer appears — is what makes the later coexistence behavior a change in habit rather than an emitter rewrite.

### The always-on strict breaking-change gate — `buf breaking` for proto, a custom JSON-diff gate for the rest, with a per-break force valve

The gate is **wired now**, in full, with both halves path-filtered, pull-request-blocking, and **always enforcing**, both comparing against the baseline branch:

- **Proto** is gated by **`buf breaking` at `FILE` level**, run `--against` the integration baseline branch. `buf breaking` is the mature, purpose-built protobuf wire-compatibility checker and covers the proto half of the catalog directly — over the now-correctly-numbered proto its `WIRE`/`WIRE_JSON`/`PACKAGE`/`FILE` rule categories are authoritative, and those categories are exactly what makes the **break axis legible** (a `WIRE`/`WIRE_JSON` hit is a wire break; a `PACKAGE`/`FILE` hit is a source-API break that left the wire intact — see [Two version handles](#two-version-handles--wire-generation-in-the-name-release-in-the-semver)).

- **The `*.spec.json` catalogs and the i18n message keys** are gated by a **custom JSON-diff gate**, because `buf` does not cover them. Each spec key and each `TK` translation key is treated like a proto field number: **immutable once published, additive-only, deprecated-not-deleted** (via `[Obsolete]` on the generated constant rather than deletion of the key). The custom gate fails on a removed or retyped spec entry or a removed translation key the same way `buf breaking` fails on a removed field. This extends the breaking-change discipline to the catalog surfaces the proto checker is blind to.

Both halves carry a **per-break force valve** modeled on Buf's `buf skip breaking` label: an **explicit, deliberate per-break override** — a labeled pull request, a sentinel commit footer, or equivalent — is the *only* way a breaking change passes the gate. There is no defaulted-on global switch; the valve is pulled (or not) once per break:

- **Now**, while every consumer is in-repo, the valve is pulled **freely** — and pulling it is the **same conscious act** as bumping the package's semver and writing its changelog entry. The break is taken in place, the old definition is deleted, the consumers are patched in the same pull request, and everything deploys atomically.
- **Later**, once an external consumer pins, the valve is pulled **rarely** — a wire break stops being forced in place and instead mints a new named wire generation that coexists (see [Two version handles](#two-version-handles--wire-generation-in-the-name-release-in-the-semver)).

**Pre-stable channels (`v2alpha`, `v2beta`) are gate-exempt** ([Stability channels](#stability-channels--start-in-v2alpha-climb-to-stable-v2)), which is what keeps the force valve honest: because a pre-stable surface breaks freely *without* the valve, the valve only ever fires on a surface that has **graduated to stable `v2`**. That makes pulling it a meaningful conscious speed-bump — a deliberate "I am breaking a stable contract" signal — rather than rote ceremony fired on every churning prototype. The gate is a tripwire against mistakes, not a prohibition on evolution.

### Two version handles — wire generation in the name, release in the semver

A versioned package carries **two independent handles**, and conflating them is the trap this subsection forecloses.

- **Wire generation = name-encoded `vN`.** The wire-compatibility generation lives in the **package name / namespace**, not in the semver number — the googleapis / Go-module / SONAME pattern. The proto `package d2.<svc>.vN` emits a package named `D2.<Svc>.V<N>.*` on .NET and `@dcsv-io/d2-<svc>-v<N>` on TS. The generation suffix is carried **from the start** (the current generation is `V2`, after the pre-stable climb finishes), so a future `V3` is **additive — a new named package — never a rename** of `V2`. **Now**, the wire generation stays at `V2`: a wire break of a stable surface is forced in place and `V2` keeps its name. **Later**, a wire break **mints `V3` as a new named package** that coexists with `V2` — both generations are served by the one running service instance, not separate deployments (see [Coexistence mechanics](#coexistence-mechanics)).

- **Release = standard 3-part semver, per package.** Each consumable package carries a plain `MAJOR.MINOR.PATCH` that is a **valid semver string** — pin-safe on npm and NuGet alike. This is the number that goes in `<PackageVersion>` / npm `version`. It moves on every release of that package (MAJOR on a forced break, MINOR on an additive change, PATCH on a fix) independent of whether the *wire generation* in the name changed.

- **Break-axis legibility = the gate category + a changelog tag, not the number.** The semver number **cannot** encode *which* compatibility axis broke (a single integer has no room for two orthogonal majors), so the axis is carried by two other signals: the **Buf gate category that caught it** (`WIRE` / `WIRE_JSON` = a wire break; `PACKAGE` / `FILE` = a source-API break that left the wire intact) and a **Conventional-Commits changelog footer** — `WIRE-BREAKING:` for a wire break versus the plain `BREAKING CHANGE:` for a source-API break. The name and the changelog tag carry the axis; the semver number only carries "something incompatible changed."

- **Optional runtime handshake.** A `wireVersion` (equivalently `protocolVersion`) metadata field plus a runtime version handshake — the PostgreSQL / MongoDB / Kafka negotiation pattern — is built now and usable from day one for runtime compatibility checks, so a client and server can confirm a compatible wire generation before exchanging payloads.

**The 4-part composite `WIRE_MAJOR.API_MAJOR.MINOR.PATCH` is rejected.** Packing two compatibility majors into one dotted string fails three independent ways: it is **invalid semver on npm** (the parser truncates or rejects the fourth dotted component, so a published version cannot be pinned reliably); it is **semantically meaningless on NuGet** (NuGet normalization drops a trailing zero fourth segment, so `2.1.3.0` and `2.1.3` collide and the fourth field silently disappears); and it has **zero precedent** — across roughly thirteen surveyed systems (Linux SONAME, protobuf/gRPC, googleapis, PostgreSQL, MongoDB, Kafka, AMQP, TLS, SSH, TensorFlow, MCP, and more) **none** packs two compatibility majors into a single dotted string, and the long-standing semver proposal to permit a fourth identifier (semver issue #948) has been **open and unresolved since 2015**. The field's universal answer is the split this subsection takes — generation in the **name**, release in the **number** — so D² takes it too.

### Per-package bump tracking — one conscious act

Each **consumable package keeps its own `CHANGELOG.md`** with break-axis sections, and the three artifacts of a break are **one conscious act, not three chores**: pulling the per-break **force valve**, **bumping the package's semver**, and **writing the changelog entry** happen together — the keystroke that overrides the gate is the same decision that moves the number and records the why.

Granularity follows what is actually consumed:

- **Services version as deployables**, not as pinned packages — a service ships on the product cadence and nobody pins it, so it does not carry an independent consumable semver.
- **Consumable libraries carry per-package semver** — the `DcsvIo.D2.*` libraries, the `@dcsv-io/d2-*` packages, and the generated `D2.<Svc>.V<N>.*` wire packages each move their own `MAJOR.MINOR.PATCH` and keep their own changelog, because each is a unit an external adopter (today an in-repo one, tomorrow an off-repo one) pins independently.

### Now-behavior vs later-behavior — under one always-strict gate

The strategy splits on **who consumes the contract and whether they deploy atomically with it** — but the split is a difference in **behavior under the one always-strict gate**, not two modes the gate switches between. The gate is the same gate, always enforcing, in both; what changes is whether the force valve is pulled and whether old generations coexist.

#### Now-behavior — internal contracts, single live version, force-and-delete

Internal product contracts — the operations the BFF and the internal services consume, where every consumer is in-repo and deploys together — run **one live wire generation**. A breaking change of a stable surface is made by **pulling the per-break force valve**, the consumers are patched in the same change, and everything deploys atomically. **The old artifacts are deleted, not coexisted** — there is no hoarding of dead `V2` alongside a new generation when nothing external pins `V2`, and **no `reserved` is emitted on the forced break** because the deleted definition has no surviving consumer to reserve against. This is sound precisely because the deploy is atomic: the web client and a future Electron wrapper (which serves the same web application) deploy together with the server, so the instant the new shape is live, every consumer already speaks it and nothing is left broken. Coexisting generations internally would accumulate dead code to defend against a mid-deploy skew that the atomic deploy makes impossible.

**The durable record is the changelog, not the deleted code.** Even though the old artifact is deleted, **every breaking change bumps the package's semver and writes a breaking-change changelog entry — starting now.** The deleted code is gone, but the version history and the changelog preserve the fact, the reason, the axis, and the boundary of every break. The history is the institutional memory and the muscle memory; deleting the code does not delete the record of what changed. This is the discipline that keeps "force-and-delete under atomic deploy" from becoming "lose the history of every break."

#### Later-behavior — external / open-source surfaces, coexist + deprecate

When the `DcsvIo.D2.*` / `@dcsv-io/d2-*` fundamentals are extracted or open-sourced, or when **any** external consumer pins a version, the author **stops pulling the force valve** on the affected surface and runs the **full external rules**: real semantic versioning, a public changelog, a per-generation migration guide, **actual coexistence of supported wire generations** (a wire break **mints a new named `V<N+1>` package** — `V2` mints `V3` — while the old generation keeps serving; both generations are served by the **one running service instance** with its one product semver, one database, and one migration set), `reserved` **accumulating** on every delete in the retained generation, and the full deprecation lifecycle — the `Deprecation` and `Sunset` response headers, telemetry-gated removal, and a declared support window. The "services version as deployables" principle ([Per-package bump tracking](#per-package-bump-tracking--one-conscious-act)) is unaffected: the service still ships as one deployable under one product semver even while serving multiple wire generations. The single-live-generation freedom of now-behavior is explicitly **forfeited** the moment a consumer outside the deploy holds a pin, because there is no longer an atomic deploy that guarantees nothing is left broken. This machinery is **built now**; what is future is its **use** — the switch happens at the first external pin or open-source extraction, and because the machinery already exists the graduation is a change in habit, not a re-architecture. (Its two runtime pieces that physically need the Edge request pipeline — telemetry-gated removal and the `Deprecation`/`Sunset` middleware — wire in when the Edge is built; see [Build now; behavior shifts later](#build-now-behavior-shifts-later).)

##### Coexistence mechanics

**Coexisting wire generations are served by ONE service instance — one product semver, one database, one migration set.** The internal domain and handlers run only the latest generation; an older generation is a thin translation **shim** (old-wire ↔ latest-internal `<Op>Input` / `<Op>Output` mapper) at the api/transport layer, kept alive until telemetry-gated removal. Generation coexistence is a wire-surface concern and never forks the domain, the database, or the deployment — running two databases or two migration sets per service is outside the design.

The default shim shape is **one current handler plus a per-generation transport mapper**: the old generation's `api/Mappers/` adapts its wire shape to and from the current `<Op>Input` / `<Op>Output`, so the business logic runs once and only the wire translation differs per generation. A **frozen old-generation handler** is the fallback, used only when the operation's *semantics* — not just its wire shape — diverge between generations. Serving both at once: gRPC exposes distinct package-path services (`/d2.keycustodian.v2.…` vs `/d2.keycustodian.v3.…`); REST exposes distinct `/v2/…` and `/v3/…` route groups; both are registered in the Edge host.

### Carve-out — "force-and-delete under atomic deploy" is synchronous-only

The "force-and-delete under atomic deploy" freedom of now-behavior applies **only to synchronous request/response contracts**, where the old shape stops existing the moment the deploy completes. It **does not** apply to **asynchronous or persisted schemas** — RabbitMQ event protos, and any proto persisted at rest — because those can break **across the deploy window** even with no external consumer: a message published by the old code and consumed by the new code (or a row written under the old schema and read under the new) spans the deploy boundary that the synchronous atomic-deploy argument relies on. Async and persisted schemas therefore require **additive discipline or an explicit drain/migration**, even pre-launch, regardless of the synchronous freedom. This carve-out matters: it is the one place the "atomic deploy makes a forced break safe" reasoning does not reach.

### What is built now versus what requires the Edge pipeline

What **is built now is the complete system**, not a subset of it: Tolerant Reader on consumers, additive-by-default, `@typespec/versioning` annotation on production namespaces, the stability channels, the author-pinned field numbers + `reserved`-emission emitter enhancement, the always-on strict breaking-change gate (both halves) with its per-break force valve, the two-handle version-identifier scheme (name-encoded wire generation + per-package semver), the coexistence machinery, and the deprecation-lifecycle machinery — all built now (see [Built now — the complete system](#built-now--the-complete-system)). The per-package-bump-plus-changelog discipline runs now on every break. The only items not yet wired are the two **runtime mechanisms that require the running Edge request pipeline** — telemetry-gated removal (reads live request telemetry the Edge emits) and the `Deprecation`/`Sunset` response middleware (runs in the Edge's response pipeline). Their contract-side shape is built now so they are drop-in when that pipeline exists. The [Now-behavior vs later-behavior](#now-behavior-vs-later-behavior--under-one-always-strict-gate) section draws this line explicitly.

## Build now; behavior shifts later

The strategy is **not** delivered in construction phases. The **entire system is built now**; the difference between today and the external-pinning future is a **change in behavior** — how often the force valve is pulled, whether old generations coexist — **not what gets built**, and **not a mode the gate switches between** (the gate is always strict). The single genuine build-later item is the pair of pieces that need a running Edge request pipeline.

### Built now — the complete system

This is the construction commitment: every piece below is built now, none stubbed and none deferred by choice.

- `@typespec/versioning` modeling (`@added`/`@removed`/`@renamedFrom`/`@madeOptional`/`@typeChangedFrom`) on the production namespaces, with per-version artifacts emitted from the annotations.
- The `v2alpha`/`v2beta`/`v2` stability channels, with pre-stable surfaces breaking-gate-exempt at `0.x` semver and stable `v2` gate-enforced from `1.0.0`.
- The author-pinned field-number (`@d2Field(n)`-style decorator + authored reserved list) + `reserved`-emission emitter enhancement.
- The always-on strict breaking-change gate — `buf breaking` (`FILE` level, `--against` baseline) for proto plus the custom JSON-diff gate for `*.spec.json` and i18n keys — with the per-break force valve.
- The two-handle version-identifier scheme — name-encoded `vN` wire generation (`D2.<Svc>.V<N>.*` / `@dcsv-io/d2-<svc>-v<N>`) plus per-package 3-part semver, with the optional `wireVersion` metadata field + runtime handshake.
- The coexistence machinery — the ability to emit and serve more than one named wire generation side by side.
- The deprecation-lifecycle machinery — the `Deprecation`/`Sunset` header shape, the support-window policy structure, and the telemetry-gated-removal logic, all built on the contract side so they are drop-in when the Edge pipeline exists.
- The per-package-bump-plus-breaking-change-changelog discipline, which runs now on every break.
- Tolerant Reader as an asserted property of the generated consumers.

### The behavior shift — force-and-delete now, coexist-and-deprecate later

There is **no mode switch and no force-defaulted-on configuration**. The gate is always strict; what shifts is the team's *behavior* against it, triggered by the **first external consumer pin OR the first open-sourced lib**.

- **Now-behavior.** Every break of a stable surface is consciously **forced per-break** (the same act as the semver bump + changelog entry); the break is taken **in place** (the wire generation stays `V2`), the old definition is **deleted** (delete-don't-coexist), `reserved` is **not** emitted on the forced break (nothing surviving to reserve against), and the deprecation lifecycle (the `Deprecation`/`Sunset` headers, telemetry-gated removal, support windows) is **dormant**. The per-package semver moves and the changelog records every break — that habit runs now. (Pre-stable `v2alpha` / `v2beta` surfaces stay gate-exempt and break freely at `0.x` without the valve.)
- **Later-behavior.** The author **stops forcing** on the affected surface: a wire break **mints a new named `V<N+1>` package** (`V2` mints `V3`) that coexists with the retained old generation — both served by the one instance, one database, one deployment (the retained generation becomes a translation shim over the current internal model); `reserved` **accumulates** on every delete in the retained generation and pinned field numbers are honored as **immutable**; and the full deprecation lifecycle plus declared support windows **activate**.

The [now-behavior vs later-behavior](#now-behavior-vs-later-behavior--under-one-always-strict-gate) section above describes these as behaviors of the one always-strict system; the difference between them is a change in habit at the trigger, not a re-architecture.

### The two pieces that require the Edge request pipeline

Two pieces are not yet wired because they **require a running Edge request pipeline** — a structural prerequisite, not a scope choice:

- **Telemetry-gated removal** (the contract step driven by observed zero usage) — it reads request telemetry the Edge emits; it wires in when that pipeline is present.
- **The `Deprecation` (RFC 9745) and `Sunset` (RFC 8594) response-header middleware** — it runs in the Edge's response pipeline; it wires in when that pipeline is present.

Their **contract-side shape is built now** (header semantics, support-window policy structure, the zero-usage-removal logic), so wiring them into the Edge pipeline is a drop-in, not a design.

## Out of current scope

Every strategy decision here is **made** and recorded as settled in the Decision above — field numbers are **author-pinned** (a `@d2Field(n)`-style decorator + an authored reserved list), REST versions by **URL-path `/v2/…`** mirroring the proto `vN` package, an internal wire break of a stable surface is **forced in place at `v2` + delete-old**, and the release identifier is a **per-package 3-part semver** with the wire generation carried name-encoded as `vN` (the two-handle scheme). Exactly one _parameter_ is not yet fixed — not because the choice is unresolved, but because committing a concrete number prematurely would be guesswork:

- **External support-window length (e.g. 24 months).** The _mechanism_ — a declared, written support window for externally-pinned versions — is decided. Only the concrete _number_ is left open: it is sensibly set once there is a product and release cadence to anchor it to.

## Implementation note — renumber the committed protos

The committed protos were renumbered to `d2.keycustodian.v2alpha` — the proto package (`d2.keycustodian.v2alpha`) and the generated wire C# namespace (`D2.Services.Protos.KeyCustodian.V2Alpha`) both carry the stability channel per AIP-185. No published `.NET` or TS package carries the channel in its ID (the stability channel is carried by the semver pre-release label and the npm dist-tag / NuGet pre-release flag, not the package name). The renumber ran through the normal codegen pipeline: the emitter derived the new proto package and C# namespace from the renumbered TypeSpec source, the `tspconfig.yaml` fields updated, emitted artifacts regenerated, and downstream references updated in the same change.

## In practice — worked examples

Each example below is grounded in the real KeyCustodian module — the `GetJwks` operation and message fields such as `kid` / `payload` / `signature`. The examples follow the full arc: a surface starts in `v2alpha` (gate-exempt, `0.x`), climbs `v2alpha → v2beta → v2`, then runs under the stable gate. Decorators marked *(to-be-built)* are conventions this ADR adopts; the `@d2Field(n)` pin is one of them.

### 1. Pre-stable `v2alpha` — break freely, gate-exempt

Essentially every surface sits here today. The operation is authored in TypeSpec with field-number pins from the start, but lives in the `v2alpha` channel:

```typespec
@versioned(KeyCustodian.Versions)
namespace D2.KeyCustodian.V2Alpha;

model SigningKey {
  @d2Field(1) kid: string;        // @d2Field(n) — author-pinned wire number (to-be-built)
  @d2Field(2) payload: bytes;
}
```

```proto
syntax = "proto3";
package d2.keycustodian.v2alpha;
```

The shape may be reshaped, retyped, and renumbered **freely** — the gate is **exempt** for pre-stable channels, so **no force valve is pulled** (its instability is declared in its name). The package sits at `0.x`, semver's own "anything may change" range. A break here bumps the **MINOR** and writes a changelog entry. **Version/changelog action:** MINOR bump `0.4.0 → 0.5.0`; a `### Changed` entry; **no force valve.**

### 2. Graduation — `v2alpha → v2beta → v2`, cut `1.0.0` at stable

When the shape settles it is **promoted (renamed)** up the ladder — `v2alpha → v2beta`, then `v2beta → v2`. Each promotion is a rename allowed **without a force-break**, because a pre-stable surface carries no compatibility promise; in-repo consumers migrate atomically at the promotion:

```text
package d2.keycustodian.v2alpha   (0.x, gate-exempt)
        ↓  promote (rename, no force valve)
package d2.keycustodian.v2beta    (0.x, gate-exempt)
        ↓  promote (rename, no force valve)
package d2.keycustodian.v2        (1.0.0 — first stable release, gate-enforced)
```

At graduation to stable `v2`, the package cuts **`1.0.0`** (`D2.KeyCustodian.V2.Grpc @ 1.0.0`, `@dcsv-io/d2-keycustodian-v2@1.0.0`). From this point the breaking-change gate **enforces** against it.

### 3. Stable `v2`, additive change — new optional field

The surface is now stable `v2`. Add an optional field with a fresh `@added` marker and a fresh pinned number; no existing consumer reads it:

```typespec
model SigningKey {
  @d2Field(1) kid: string;
  @d2Field(2) payload: bytes;
  @d2Field(3) signature: bytes;
  @added(KeyCustodian.Versions.v2_1) @d2Field(4) algorithm?: string;
}
```

`buf breaking` **PASSES** (a new field number is additive). **Version/changelog action:** MINOR bump `1.0.0 → 1.1.0`; a changelog `### Added` entry; **no force valve, no package-name change**.

### 4. Stable `v2`, forced wire break (now-behavior)

Retype `payload` from `bytes` to a structured message — a wire break of a stable surface. `buf breaking` **BLOCKS** with a `WIRE` finding. Under now-behavior the author pulls the **per-break force valve** and takes the break **in place**:

```text
# PR label form:
versioning:force-break

# or commit footer form:
WIRE-BREAKING: SigningKey.payload retyped bytes -> EncodedKey (atomic deploy, V2 in place)
```

The proto **stays `package d2.keycustodian.v2`**, **no `reserved`** is emitted (the old definition is deleted outright — nothing surviving to reserve against), every in-repo consumer is patched in the **same pull request**, and the whole change **deploys atomically**. **Version/changelog action:** MAJOR bump `1.1.0 → 2.0.0`; changelog entry tagged `WIRE-BREAKING:`; **package name stays `.V2`** (`D2.KeyCustodian.V2.Grpc @ 2.0.0`).

### 5. Stable `v2`, source-API-only break — wire intact

Rename the response **message type** `JwksResponse` → `SigningKeysResponse`, leaving every field name and number untouched. proto3 keys the binary wire by field *number*, and the message-type name rides neither the binary nor the JSON wire — so the bytes are identical and an old client decodes them fine. What changed is the generated C#/TS **type name**, so `buf breaking` flags the rename under `PACKAGE` / `FILE` (a deleted-message symbol + a changed RPC response type) — **not** `WIRE`. The author pulls the force valve:

```text
BREAKING CHANGE: rename message JwksResponse -> SigningKeysResponse (generated type name; wire bytes unchanged)
```

**Version/changelog action:** MAJOR bump; changelog entry tagged `BREAKING CHANGE:` (API). This is the concrete wire-vs-API distinction: same MAJOR bump, **different gate category and different changelog tag** carry the axis the semver number cannot.

### 6. Later / strict — first external consumer mints `v3`

An external partner now pins `@dcsv-io/d2-keycustodian-v2`. The author **stops forcing** on this surface. A wire break no longer happens in place — it **mints a new named wire generation that coexists**:

```text
D2.KeyCustodian.V2.Grpc  →  retained, still served, reserved now accumulates on deletes
D2.KeyCustodian.V3.Grpc  →  new wire generation (package d2.keycustodian.v3), semver 1.0.0
@dcsv-io/d2-keycustodian-v2      →  retained
@dcsv-io/d2-keycustodian-v3      →  new, version 1.0.0
```

`V3` starts its own per-package semver at `1.0.0` (the wire generation lives in the **name**, not the number). `V2` keeps serving, `reserved` accumulates on any `V2` delete, `V2` responses begin emitting `Deprecation` (RFC 9745) / `Sunset` (RFC 8594) headers, and `V2` removal waits on **telemetry-gated zero usage** — not a date. The `V2 → V3` hop is a **new package alongside the old**, never a rename of `V2`.

### 7. A package `CHANGELOG.md` excerpt + the decision table

Each consumable package keeps its own changelog with break-axis sections:

```markdown
# Changelog — @dcsv-io/d2-keycustodian-v2

## 2.0.0

### Wire-breaking

- `SigningKey.payload` retyped `bytes` → `EncodedKey` (forced in place; atomic deploy).

### API-breaking

- Renamed message `JwksResponse` → `SigningKeysResponse` (generated type name; wire bytes unchanged).

## 1.1.0

### Added

- `SigningKey.algorithm` (optional, field 4).

## 1.0.0

- First stable release (graduated from `v2beta`).
```

| Change type             | Buf category       | Force valve? | Semver bump        | Package-name change?    | `reserved`?       | Now vs later |
| ----------------------- | ------------------ | ------------ | ------------------ | ----------------------- | ----------------- | ------------ |
| Pre-stable churn        | exempt             | no           | MINOR (`0.x`)      | no                      | n/a               | same both    |
| Graduate to stable `v2` | (promote/rename)   | no           | `1.0.0` cut        | no — published ID unchanged; proto package + wire C# namespace drop the channel suffix (`…v2alpha → …v2`); version cuts `1.0.0`, dist-tag flips to `latest` | n/a               | same both    |
| Add optional field      | (passes)           | no           | MINOR (`≥1.0.0`)   | no                      | n/a               | same both    |
| Wire break, in place    | `WIRE`/`WIRE_JSON` | yes          | MAJOR              | no (stays `.V2`)        | no (old deleted)  | **now**      |
| Source-API-only break   | `PACKAGE`/`FILE`   | yes          | MAJOR              | no                      | n/a               | same both    |
| Wire break, coexist     | `WIRE`/`WIRE_JSON` | no           | MAJOR (new)        | yes (`.V2` → new `.V3`) | yes (accumulates) | **later**    |

## Consequences

**Positive.**

- **Accidental wire breaks become impossible.** The always-on strict gate — `buf breaking` over a now-correctly-numbered proto plus the custom JSON-diff gate over the catalogs — blocks the renumbered field, the silently-reused number, the dropped enum member, and the removed spec or translation key before they merge. The human "when in doubt, bump" is replaced by a machine guarantee, and the intentional break is still possible through the explicit per-break force valve.
- **The proto wire is finally honest.** Author-pinned field numbers plus `reserved` emission close the two latent corruption classes the positional emitter carries today (renumber-on-reorder, reuse-on-delete), which also makes the proto gate meaningful rather than theatrical.
- **One system built now; the first external pin is a habit change, not a rewrite.** The complete system — always-strict gate, pinning, two-handle identifier scheme, coexistence machinery, deprecation-lifecycle machinery — is built now. Now-behavior is force-and-delete under atomic deploy (one live wire generation, no dead-generation hoarding) and later-behavior is coexist-and-deprecate (a wire break mints a new named generation, `reserved` accumulates, the deprecation lifecycle activates). Because the later-behavior machinery is already built, the shift at the first external pin is a **change in habit** — stop pulling the force valve, start coexisting — rather than a re-architecture. The institutional record survives the deletion because every break bumps the package semver and writes a changelog entry from day one.
- **The version identifier never lies and never breaks tooling.** Carrying the **stable** wire generation in the **published package name** (`D2.<Svc>.V<N>.*` / `@dcsv-io/d2-<svc>-vN`) and the release in a **plain 3-part semver** keeps every published version a valid, pin-safe string on npm and NuGet, while the gate category + changelog tag carry the break axis the number cannot — avoiding the invalid-semver / NuGet-normalization / zero-precedent failures of a 4-part composite. The pre-stable stability channel (alpha/beta) rides the **semver pre-release label** (`0.x` / `1.0.0-alpha.N`) and the npm dist-tag / NuGet pre-release flag, not the published package ID, so the package name is stable through the pre-stable climb and only the stable generation is ever name-encoded in a published ID.
- **Versioning stays inside the single-source-of-truth guarantee.** Modeling version evolution in `@typespec/versioning` and emitting per-version artifacts means the proto, OpenAPI, DTO, and spec views of a version cannot drift, exactly as the operation itself cannot drift across representations.
- **Grounded in established practice.** Every layer maps to a named industry pattern (AIP-180/185, protobuf best practices, Buf, Confluent compatibility modes, RFC 8594/9745, Parallel Change, Tolerant Reader, Pact, Stripe/GitHub/Shopify, SONAME / googleapis / Go-module name-encoded generations), so the strategy inherits the field's accumulated experience rather than inventing its own.

**Negative / trade-offs.**

- **A meaningful gate depends on the emitter pinning field numbers and emitting `reserved`.** That capability is built now alongside the gate, so the gate enforces from day one. It is a reversal of the prior `reserved` deferral that adds emission the old policy explicitly chose to omit — taken now so later-behavior is a change in habit rather than an emitter rewrite.
- **A second, custom gate must be built and maintained.** `buf breaking` does not cover the `*.spec.json` catalogs or the i18n keys, so the JSON-diff gate is bespoke code the team owns — the price of extending wire-compatibility discipline to the catalog surfaces.
- **Two behaviors are a real cognitive split.** Contributors must know whether a contract is in now-behavior (single-live, force-and-delete) or later-behavior (coexisting, deprecated), because the deletion freedom and the deprecation lifecycle differ sharply between them, and the synchronous-only carve-out adds a third axis (a synchronous internal contract may be force-broken freely, but an async or persisted one may not).
- **Deleting old internal artifacts forfeits in-code history.** The per-package changelog-and-semver-bump discipline is what preserves the record; if that discipline lapses on a break, the history of that break is genuinely lost, because the code that embodied it is gone. The strategy's integrity leans on the changelog discipline being honored every time — which is why the force-valve keystroke, the semver bump, and the changelog entry are framed as one act.
- **Two pieces require the Edge request pipeline.** The deprecation-lifecycle machinery is built now on the contract side, but its two runtime pieces — telemetry-gated removal and the `Deprecation`/`Sunset` response middleware — need the Edge request pipeline (telemetry to read, a response pipeline to attach a header to), so they wire in when that pipeline exists. Later-behavior is fully designed and its contract-side shape is drop-in.
- **Stability channels add surface even pre-alpha.** Carrying `v2alpha`/`v2beta`/`v2` channels while almost everything is pre-stable is a small up-front cost taken to avoid the larger cost of retrofitting the distinction after surfaces ship as a bare stable name — and it is what keeps the force valve firing only on surfaces that have graduated to stable `v2`.

## Alternatives considered

- **Keep all wire generations coexisting, internally included.** Defend against mid-deploy skew by never deleting an old generation — run `V2` and `V3` of an internal service side by side. Rejected for internal contracts per the atomic-deploy reality: in-repo consumers deploy together, so there is no skew window for a coexisting old generation to protect, and hoarding dead generations accumulates code that exists only to guard an impossible state. Coexistence is retained where it is actually needed — later-behavior, where a consumer outside the deploy genuinely pins an old generation.

- **Positional field-numbering forever.** Leave the emitter assigning field numbers by declaration order and not emitting `reserved`. Rejected: it would make the proto gate theater (an enforcing gate would still pass renumber-on-reorder and reuse-on-delete, the very breaks it exists to catch) and leaves two silent wire-corruption classes live. Author-pinning the numbers + emitting `reserved` now is what makes the gate meaningful from day one and makes later-behavior a change in habit rather than the emitter rewrite a deferred enhancement would force.

- **Emitter-managed field-number registry.** Have the emitter auto-assign the next free number to a new field and auto-emit `reserved` on removal, persisting a field-number registry it maintains. Rejected in favor of author-pinning: it is more ergonomic (the author never picks a number) but introduces a stateful build artifact that must itself be committed, reviewed, and kept consistent, and it weakens the reviewable-diff property that author-pinned numbers in the `.tsp` give for free.

- **A 4-part composite version `WIRE_MAJOR.API_MAJOR.MINOR.PATCH`.** Encode both compatibility majors in one dotted string so the number alone tells you which axis broke. Rejected on three independent grounds: it is **invalid semver on npm** (the fourth dotted component is truncated or rejected, breaking pins), **semantically meaningless on NuGet** (normalization drops a trailing-zero fourth segment, so `2.1.3.0` and `2.1.3` collide), and has **zero precedent** across ~13 surveyed systems (the semver proposal to allow a fourth identifier, issue #948, has been open since 2015). The two-handle split — generation in the name, release in the number, axis in the gate category + changelog tag — is the field's universal answer and the one this ADR takes.

- **No versioning until launch.** Defer the whole apparatus — channels, gate, annotations, changelog — until there is a public product to version. Rejected: it loses the institutional history of every break made in the interim and the team's muscle memory for the discipline, and it makes the eventual adoption a large retrofit (surfaces already shipped as a bare stable name with no pre-stable channel behind them, no `reserved` in the proto history, no changelog of past breaks) rather than the build-now adoption this ADR takes, where the system exists and enforces from day one and only the team's behavior against it shifts at the first external pin.

- **Date-based versioning (Stripe / GitHub) for REST.** Version REST by a dated, pinned scheme (a request date header or dated version identifiers) rather than a `vN` URL path mirroring the proto package. Considered and **set aside** in favor of URL-path `/v2/…`: a dated scheme supports fine-grained per-consumer pinning and is proven at scale, but it introduces a second versioning idiom alongside the proto `vN` packages, whereas URL-path versioning keeps one `vN` scheme across both transport planes.

## Amendment — 2026-06-26: Artifact-diff engine, source-based fingerprints, committed baselines, fingerprint propagation, and `--graduate`

The original ADR described the bump mechanism as a "one conscious act" of pulling the force valve + bumping the package's semver + writing the changelog entry. That description fit the initially-planned commit-type model. The shipped implementation replaces commit-type as the **default** bump source with a build-free artifact-diff engine. The five items below amend the ADR to reflect what actually shipped.

### 1. Artifact-diff engine is the default bump source

The `public/tools/release-runner` default path (`diff-runner.ts:runDiffRelease`) derives each package's bump from a **build-free artifact diff**, not from commit footers. The diff has two inputs:

- **Source-based output fingerprint** — a SHA-256 hash over the committed source files, the committed public-API report, the resolved dependency versions, and the declared toolchain pin. The fingerprint is byte-identical across machines and operating systems because it hashes committed text only, with no build output involved.
- **Public-API-surface text diff** — a git-ref diff of the committed API report (`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` for .NET, `etc/<pkg>.api.md` for TypeScript) against the previous baseline.

These two inputs determine the **floor bump**: any fingerprint change → at least PATCH; any API surface change → at least MINOR. **Commit footers (`WIRE-BREAKING:` / `BREAKING CHANGE:`) are escalation overrides** — they can raise the diff-derived floor to MAJOR, but they cannot lower it. The commit type (`feat`/`fix`/etc.) is demoted to changelog categorization only; it no longer drives the bump magnitude.

The original "one conscious act" framing still applies to wire/API breaks that the diff cannot detect: the force-valve footer is still the ONE conscious act for those, and it drives both the gate valve and the bump escalation.

### 2. Committed baseline files are pipeline output, not hand-authored

Each consumable package carries committed baseline files that the seed scripts generate and the diff engine reads:

- `.release-fingerprint` — a single-line 64-hex SHA-256, committed alongside the `.csproj` (for .NET) or `package.json` (for TypeScript, under `etc/`).
- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` — .NET public-API baselines (generated by `Microsoft.CodeAnalysis.PublicApiAnalyzers`).
- `etc/<pkg>.api.md` — TypeScript public-API report (generated by `@microsoft/api-extractor`).

These files are **pipeline output** — generated by the seed scripts (`seed-publicapi-baselines.mjs`, `seed-apiextractor-baselines.mjs`), committed to the repo, and regenerated by running those scripts. They are **never hand-edited**. The drift-check lane in CI (`drift-check-cli.ts`) detects when a committed baseline diverges from the current source.

### 3. `--legacy-commit-type` is a one-cycle escape hatch

The original commit-type bump model (commit type → PATCH/MINOR; break footer → MAJOR) is retained in the runner behind `--legacy-commit-type` (`runner.ts:runRelease`). It is **not the default**. It exists as a rollback escape hatch for one release cycle and is expected to be removed once the artifact-diff engine has proven out over real releases.

### 4. Propagation via manifest fingerprint, not BFS

Under the artifact-diff engine, propagation to dependents is inherent in the fingerprint: when a dependency bumps, its new resolved version is folded into the dependent's fingerprint DEPS input, which changes that fingerprint and floors the dependent at PATCH. There is **no separate BFS propagation pass** in the diff path — that mechanism exists only in the legacy `runner.ts` path. The `--no-propagate` flag suppresses forwarding the resolved-version map to dependents.

### 5. `--graduate <pkg>` for the `0.x → 1.0.0` transition

The `0.x → 1.0.0` graduation — promoting a pre-stable package to its first stable release — is a **deliberate, explicit act** using the `--graduate <pkg>` flag. It is never inferred automatically from the diff or from a break. Graduation activates the strict force valve and MAJOR bump for subsequent breaking changes on that package.

---

## References

- [ADR-0021](0021-unified-operation-contract-idl.md) — the unified operation-contract IDL with TypeSpec as the single source of truth and proto/OpenAPI as emitted outputs; this versioning strategy is modeled in that same TypeSpec source (via `@typespec/versioning`) and the field-number-pinning enhancement is an addition to that ADR's proto emitter.
- [ADR-0019](0019-wrapped-result-wire-model.md) — the wrapped-result wire model the versioned wire DTOs ride; an evolving contract carries its `D2Result` envelope unchanged across versions.
- [ADR-0018](0018-spec-driven-error-codes.md) — the spec-driven error codes whose `*.spec.json` catalog the custom JSON-diff gate guards as immutable-once-published, additive-only entries.
- [ADR-0009](0009-async-messaging-encrypted-payloads.md) — the async messaging model whose event protos fall under the synchronous-only carve-out: they can break across the deploy window and require additive discipline or an explicit drain/migration even pre-launch.
- [ADR-0002](0002-spec-driven-codegen.md) — the spec-driven-codegen precedent whose committed-`.g.*` outputs and hand-mirror prohibition the versioning of the catalogs extends.

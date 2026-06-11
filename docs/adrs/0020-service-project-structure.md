<!--
Copyright (c) DCSV. All rights reserved.
-->

# ADR-0020: Service-project structure standard — the five-project shape, the two-section app split, per-operation handler folders, and the uppermost-node mapper rule

- **Status**: Accepted
- **Date**: 2026-06-10
- **Deliverable**: `0016-keycustodian`

## Context

Every service project under `server/services/` needs one structural shape: where the domain model lives, where handlers and ports live, where adapters and the composition root live, where transport mapping lives, and how the layers depend on each other. Without a written standard, each service re-derives the shape — and the derivations drift.

The drift is concrete and costly:

- **An anemic domain.** Pure logic — key generation, smoke verification, JWK projection, kid minting — accumulates in the app layer behind handler-shaped or strategy-interface wrappers it never needed. The domain ends up holding only data, while the behavior that operates on that data lives a layer up, untestable without the orchestration machinery around it.
- **Mappers with undocumented homes.** A service has several distinct mapping surfaces (transport wire ↔ handler I/O, persistence row ↔ domain aggregate, vendor SDK ↔ domain, messaging event ↔ domain, primitive ↔ value object). With no rule for which layer owns which, the same kind of mapper lands in the app layer in one service and the api layer in another — the reader can never predict where to look.
- **Provider folders named three different ways.** One capability is named for the capability (`WhoIs/`), another for a generic wrapper (`providers/email/resend/`), a third for the library (`auth/better-auth/`). The folder shape carries no transferable signal.
- **DTO buckets.** A flat `Models/` folder collects every input, output, and projection record with no per-shape owner — the reader cannot tell which operation owns which DTO.
- **Deep, letter-coded handler trees.** A handler's interface and implementation split across two mirror trees (`Interfaces/.../C/IRotateKey.cs` and `Implementations/.../C/RotateKey.cs`), six folder levels deep, with single-letter category folders (`C/Q/U/X`) that collide visually with the CRUD `C/R/U/D` letters used elsewhere.

Earlier service layouts got the bones right — per-operation handler folders with co-located DTOs, a domain home for pure rules, an app-owns-all-ports rule, vendor subfolders under providers — but documented none of it, so the next service re-invented worse shapes. This ADR takes the parts that worked, fixes the parts that hurt, and writes the result down once so no future service has to re-derive it. **Every rule carries a one-line WHY** — the rationale is the load-bearing part; a convention nobody can justify is a convention nobody keeps.

This ADR is the structural complement to [ADR-0017](0017-ef-as-ddd-persistence.md) (EF-as-DDD persistence): ADR-0017 retired the per-op Repository handler and put the `DbContext` contract + flat record + pure mapper in the app layer; this ADR names where that record, mapper, and `DbContext` port physically live (`app/Infrastructure/Persistence/`) and why the persistence mapper stays in app rather than infra.

## Decision

A service is a fixed set of layered projects with a fixed internal shape, governed by one dependency-direction law. The rules below are the standard.

### The full service shape — five projects + clients + the source-gen shell

The canonical full service is **five runtime projects** plus one or more consumer-facing client packages plus, when it owns spec-driven error codes, one `netstandard2.0` source-gen shell. This is the default shape for every standalone deployable service.

| Project | csproj pattern | SDK | Role | Required for a standalone service? |
| ------- | -------------- | --- | ---- | ---------------------------------- |
| `domain/` | `D2.<Area>.<Service>.Domain` | `Microsoft.NET.Sdk` | Pure domain — entities, value objects, enums, rules, generated error codes | Yes |
| `app/` | `D2.<Area>.<Service>.App` | `Microsoft.NET.Sdk` | Orchestration — handlers, observability, ports, persistence/config shapes | Yes |
| `infra/` | `D2.<Area>.<Service>.Infra` | `Microsoft.NET.Sdk` | Adapters implementing the app's ports (the only vendor-SDK-touching layer) | Yes |
| `api/` | `D2.<Area>.<Service>.Api` | `Microsoft.NET.Sdk.Web` | Composition root — `Program.cs` + host wiring + transport adapters (gRPC/REST/SSE) + transport mappers | Yes (omitted only for a module-within-host) |
| `tests/` | `D2.<Area>.<Service>.Tests` | `Microsoft.NET.Sdk` | One test project per service — `Unit/` + `Integration/` mirroring source | Yes (a module-within-host's tests live in the host's test project under a module subtree) |
| `clients/dotnet/` | `D2.<Area>.<Service>.Client` | `Microsoft.NET.Sdk` | Consumer-facing client library — tiered cache, consumers, publish interfaces | When other services consume this one |
| `error-codes-source-gen/` | `D2.<Area>.<Service>.ErrorCodes.SourceGen` | `netstandard2.0` (Roslyn) | Generator shell emitting the domain's error-code constants + failure factories | When the service owns a per-domain error-code spec |

`<Area>` is the deployment grouping (`Edge`, or the service's own name for a standalone service). The composition root is the `api/` project. **WHY an explicit `api/` layer (not "the host is out of scope"):** a standalone service's `Program.cs`, its transport adapters, and its wire-shape mappers ship and deploy with it, are tested by its integration suite, and are the seam every cross-service caller hits — declaring them out-of-scope leaves every service to re-derive the same wiring, which is exactly the drift this ADR exists to stop.

### The dependency-direction law

```
Domain  ←  App  ←  Infra  ←  Api          (Tests reference what they test; Clients reference contracts + shared libs only)
```

- **Domain references nothing but shared primitives** — `D2.Shared.Result`, the generated `TK.*` / `<Service>Failures` from `D2.Shared.I18n`, `D2.Shared.Utilities` (guards/extensions/`[RedactData]`), `D2.Shared.Time` (NodaTime + `IClock`), `System.Security.Cryptography` (BCL crypto is ambient), and pure shared VO/catalog libs it composes. **No** `Microsoft.Extensions.Options`, **no** `Microsoft.EntityFrameworkCore`, **no** logging framework, **no** DI container. **WHY:** the domain is the one layer that must stay testable with zero infrastructure and reusable across transports; a single `IOptions<>` or `DbContext` reference there metastasizes.
- **App references Domain** + the handler/result/cache/messaging-abstraction shared libs + EF Core types (the `DbContext` interface is an app-owned port — see [ADR-0017](0017-ef-as-ddd-persistence.md)). App declares ports (interfaces infra implements) and shapes (persistence records, options POCOs) but never the concrete adapter, and is transport-agnostic — it speaks `<Op>Input`/`<Op>Output`, never proto/REST/SSE shapes. **WHY:** App orchestrates; it names what it needs from the outside world without binding to any one implementation or transport.
- **Infra references App** (to implement its ports) **and Domain** (the EF config maps domain types) + the concrete vendor/transport libs (`Npgsql`, `RabbitMq`, `Resend`, …). Infra is the only layer allowed to touch a concrete vendor SDK. **WHY:** vendor churn (swap Resend → SES, RabbitMQ → Kafka) must touch exactly one layer.
- **Api references App + Infra** and is the composition root + transport boundary. **Api is the only project allowed to reference Infra.** **WHY:** the composition root is the one place every concrete adapter and transport binding is named; concentrating that in `api/` keeps App and Infra free of "who composed me" knowledge.

The tie-breaker for a genuinely ambiguous placement: *"which layer can still compile if I delete the layer below the candidate?"*

### Domain layer — `Entities` / `ValueObjects` / `Enums` / `Rules`

The domain holds the business model and the pure logic over it, in four folders (plus `Errors/` + `Generated/` when codegen is present):

- `Entities/` — aggregate roots, state-machine sum-types (abstract base + sealed per-state records — see [ADR-0017](0017-ef-as-ddd-persistence.md)), and audit entities. One home for "things with identity and lifecycle."
- `ValueObjects/` — immutable smart-constructor VOs (`Create(...) → D2Result<T>`, private ctor, `FromTrusted` for the persistence read side).
- `Enums/` — closed enums backing discriminators + taxonomies.
- `Rules/` — **pure, stateless, no-IO domain logic that extends entities/VOs.** A piece of logic with no injected port and no IO (no DB, no network, no file, no DI-resolved clock — `IClock` is a permitted *method parameter*) is domain logic; when it is *behavior over* the model (a decision rule, a generator, a projection, a factory) rather than a method intrinsic to one type, it lives here.

**WHY a `Rules/` folder:** pure crypto-over-domain (key generation), pure verifiers (smoke testing), pure projections (SPKI bytes → an RFC 7517 JWK), and pure minters (kid derivation from RNG) need no port and no IO — they belong in the domain. Hosting them in the app behind interfaces makes them un-unit-testable without the handler machinery and leaves the domain anemic. A rule carries no DI, no `IOptions<>`, no logger; a tunable (RSA modulus size) is a method parameter the handler passes in, not an `IOptions<>` the rule reads. A rule that "wants" a logger or options is not a rule — it is a handler.

### App layer — the two-section split

The app project has exactly two top-level sections plus the DI extension at the `Application/` root:

```
app/
├── Application/
│   ├── Handlers/
│   │   ├── Commands/<Operation>/       # one folder per command operation
│   │   └── Queries/<Operation>/        # one folder per query operation
│   ├── Observability/                  # <Service>Log + <Service>Metrics
│   └── <Service>AppServiceCollectionExtensions.cs   # AddD2<Service>App()
└── Infrastructure/
    ├── <Concern>/                      # ports + shapes, grouped by capability concern
    └── Configuration/<Service>Options.cs            # options POCO with SECTION const
```

**WHY two sections.** `Application/` is "what this service *does*" (the operations). `Infrastructure/` is "what this service *needs from the outside*" (the ports + the shapes those ports speak). The split makes the infra project a structural mirror: every concern folder in `app/Infrastructure/` has a same-named folder in `infra/` holding the adapter — open the two side-by-side and the port-vs-impl correspondence is visual.

**Per-operation handler folders + the naming law.** One folder per operation under its category folder; the folder co-locates the interface, the impl, the input, and the output — all suffixed so the folder is namespace-safe:

```
Application/Handlers/Commands/RotateKey/
├── IRotateKeyHandler.cs     # IRotateKeyHandler : IHandler<RotateKeyInput, RotateKeyOutput>
├── RotateKeyHandler.cs      # sealed RotateKeyHandler : ..., IRotateKeyHandler
├── RotateKeyInput.cs
└── RotateKeyOutput.cs
```

| Artifact | Pattern |
| -------- | ------- |
| Handler interface | `I<Operation>Handler` |
| Handler impl | `<Operation>Handler` (file name = type name; the bare `<Operation>` type name is not used) |
| Input | `<Operation>Input` (always `Input`, never `Request`/`Command`) |
| Output | `<Operation>Output` (always `Output`; document-style names like `Outcome`/`Plan` are not used) |
| Operation-private record | `<Operation><Role>` (suffixed) or a `private` nested type on the handler |
| App DI extension | `<Service>AppServiceCollectionExtensions` → `AddD2<Service>App()` (lives at `app/Application/` root; registers what App owns) |
| Infra composition extension | `<Service>ServiceCollectionExtensions` → `AddD2<Service>()` (lives at `infra/Configuration/`; does binding + `ValidateOnStart`) |

**WHY co-locate:** the interface and its implementation are read together nearly always; two unrelated operations' interfaces are read together nearly never — co-locate what is read together. A split `Interfaces/` ⇄ `Implementations/` mirror puts an interface and its impl several folders apart for no navigational gain.

### Command vs Query — the binary side-effect rule

Exactly two handler categories: `Commands/` and `Queries/`. **A handler's category is determined solely by whether the operation mutates persistent/shared state — a DB write, a distributed-cache write, an external write, or a message publish. The verb in the name is irrelevant.** Side effect → `Commands/`; none → `Queries/`.

| Category | The test |
| -------- | -------- |
| `Commands/` | Mutates persistent/shared state via any of the four. "If the process dies right after, did state change persist?" → yes. |
| `Queries/` | No shared-state mutation. Same death test → no. |

There is no third category. An operation that *looks* like a query by name (`Find…`, `Get…`) but mutates as a side effect (find-or-create, cache-warm-on-read that broadcasts) is simply a `Command` wearing a query-ish verb — the side effect makes it a `Command`, the name is just naming. **WHY drop the "Complex" tier:** it had no crisp boundary against `Commands/` (both mutate), so it collected operations on a feel-based judgment the binary test makes unnecessary. `Find` vs `Get` stays *naming* guidance (`Find` = resolve, may fetch; `Get` = direct read) but no longer maps to a folder. **Local/in-memory caching does not disqualify a Query** — instance-scoped ephemeral state (a per-instance `ILocalCache`, a module-scoped `Lazy<FrozenDictionary>`) is not shared-state mutation; only distributed-cache writes (`*AndBroadcast*`, anything other instances observe) push an operation to `Commands/`.

### Concern folders + mandatory vendor/tech/protocol subfolders

`app/Infrastructure/` holds the ports + shapes grouped by **capability concern** (a PascalCase singular capability noun: `Persistence`, `Messaging`, `Email`, `Sms`, `Realtime`, `Storage`, `Outbound`, `Vault`, `Scheduling`, …). The `infra/` project mirrors those concern folders with the adapters, and **every concern folder in `infra/` has a tech/vendor/protocol subfolder — even when only one implementation exists today**:

```
infra/Persistence/Postgres/...      not  infra/Persistence/...
infra/Messaging/RabbitMq/...        not  infra/Messaging/...
infra/Email/Resend/...   +  infra/Email/Ses/...     (vendor axis)
infra/Outbound/Grpc/...  +  infra/Outbound/Rest/...  (protocol axis)
infra/Observability/                                 (infra adapters emit their own log delegates; same folder name as the App side so observability is found at the same path in both projects)
```

**WHY mandatory even for a sole impl:** the subfolder is the seam where a second vendor lands without a reshuffle — the day Postgres gains a test double or Resend gains an SES fallback, the new adapter drops into a sibling subfolder and nothing else moves. Consistency of shape across services (every infra concern has a vendor subfolder) outweighs the extra nesting level. The generic `Providers/` wrapper is replaced by concern + vendor. The concern-noun set is open-but-deliberate: adding a new concern noun is a standard amendment (this ADR plus PATTERNS.md get edited), not an ad-hoc per-service invention — the whole value is that jumping between services is immediately intuitive.

### The uppermost-node mapper rule — the five surfaces

**A mapping lives in the highest layer that actually touches the foreign representation; every layer beneath it speaks domain (or `<Op>Input`/`<Op>Output` at the handler boundary).** A service has five mapping surfaces, each with one home:

| # | Surface (foreign ↔ ours) | Home |
| - | ------------------------ | ---- |
| 1 | Transport — proto / REST JSON ↔ `<Op>Input`/`<Op>Output` | `api/Mappers/` (the host's api for a module-within-host) |
| 2 | Persistence — EF record ↔ domain aggregate | `app/Infrastructure/Persistence/` (beside the record) |
| 3 | Provider SDK — vendor type (Stripe / Resend / IpInfo) ↔ domain | `infra/<Concern>/<Vendor>/` (inside the adapter) |
| 4 | Messaging wire — spec-generated event ↔ domain values | `infra/Messaging/<Broker>/` (inside the publisher) |
| 5 | Primitives — `string` / `int` / bytes → domain VO | domain (`Create` factories + `Rules/` projections) |

Every mapper is a pure static C# 14 extension-member class (no DI, no IO), named `<ForeignType>Mapper`, in its surface's home.

**WHY transport mapping lives in `api/`, not `app/`:** proto/REST is a transport concern; the api is the uppermost node of the transport data path. Putting the mapper in app would force app to reference the generated proto types, coupling the orchestration layer to one wire format and breaking "App is reusable across transports." **WHY the persistence mapper stays in `app/`, not `infra/`:** under EF-as-DDD ([ADR-0017](0017-ef-as-ddd-persistence.md)) the handlers compose the queries and materialize the records, so app is the uppermost node of the persistence data path; the record mapper is a pure mapper over the app-owned record shape with no EF dependency, and placing it in infra is structurally impossible anyway (app may not reference infra) and would resurrect the per-query repository layer ADR-0017 retired. The resolving framing: **App speaks the query *language* (DbContext / DbSet / LINQ); Infra owns the *database* (Npgsql provider, connection, migrations, `xmin`, command timeouts) — App never knows it is Postgres.**

### Multi-provider simultaneity — the keyed-resolver pattern

A service may run two vendors of one capability at once (a payments service running Stripe and Square) or publish to multiple destinations chosen by key. The pattern is specified once, canonically: the app layer stays vendor-blind (one capability port per concern in `app/Infrastructure/<Concern>/`); infra registers keyed implementations under .NET keyed DI (one per vendor subfolder); and when the handler selects the vendor at runtime, a resolver port `I<Capability>Resolver.Get(key) → D2Result<T>` wraps `IServiceProvider.GetKeyedService<T>(key)` and maps an unknown key to a typed `D2Result` failure (not a thrown `InvalidOperationException`). A handler that statically knows its vendor injects `[FromKeyedServices("vendor")] I<Capability>` directly; the resolver is reached for only when the key is a runtime decision. For messaging, the resolver layers *on top of* the existing `[MqPub]` compile-time descriptor — each concrete publisher keeps its descriptor; the resolver only selects which already-described publisher to use. **WHY a resolver over raw keyed injection when the key is dynamic:** `[FromKeyedServices]` needs a compile-time key; a runtime key (an org's configured vendor) needs a lookup, and a bad key is operator/user data, so it must fail gracefully as a `D2Result`, not an exception.

### Options pipeline + the `[Required]`-on-struct pitfall

Configuration flows: env var `SECTION__PROP` (arrays `SECTION__N`) → `D2Env.Load()` (`__` → `:`, process-env wins) → `IConfiguration` → **binding + `ValidateOnStart` in `infra/Configuration/`** → the options POCO declared in `app/Infrastructure/Configuration/` (carrying the `SECTION` const) → app handlers consume `IOptions<T>` → the domain receives only adapted VOs/primitives (the domain never references `Microsoft.Extensions.Options`). **WHY split the shape from the binding:** the POCO is what handlers consume (an app concern); the binding touches `IConfiguration` + the validation pipeline (a composition concern) and fails fast at boot.

`[Required]` on a non-nullable struct (`TimeSpan Cadence`) is a no-op — a value type is never "missing," so `ValidateDataAnnotations` never fires. Use a real range validator (`[Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]`) or a custom `.Validate(…)` predicate, and let the domain VO's smart constructor be the second floor. **WHY belt-and-suspenders:** the options validator catches config errors at boot; the domain VO catches every path (config, future API, test) — `[Required]` is the trap that looks like validation but is not.

### Namespaces keep the layer segment + the global-usings policy

The namespace is the folder path verbatim, **including** the `.App` / `.Infra` layer segment (`D2.Edge.KeyCustodian.App.Infrastructure.Persistence`, `D2.Edge.KeyCustodian.Infra.Persistence.Postgres`). The layer segment is not dropped via `RootNamespace` tricks. **WHY:** in a service the layer IS semantics — `.App.Infrastructure.Persistence` (a port) versus `.Infra.Persistence.Postgres` (an adapter) is a meaningful distinction the namespace should carry; collapsing it saves one segment but creates a rule every reader must memorize.

Global usings follow a two-tier-plus-safety-set policy. **Tier-1, service-project scope** (central `<Using>` items in `server/services/Directory.Build.targets`): `D2.Shared.Result`, `D2.Shared.Utilities.Extensions`/`.Attributes`/`.Enums`, `D2.Shared.I18n`. Service projects (`server/services/`) reliably reference the full D2 runtime stack, so all five Tier-1 namespaces resolve with no per-project opt-out. **Shared libs are excluded** (`server/shared/dotnet/`) — `D2.Shared.I18n` is split across three assemblies (`I18n.Abstractions`, `I18n.Keys`, `I18n` core), and the Tier-1 libs form a dependency chain among themselves; a blanket runtime-wide global would produce hard CS0246 errors in the libs that sit below the full stack. Shared libs keep explicit usings. The `netstandard2.0` condition in the targets file excludes source-gen shells under a service. **Tier-2, per layer** (`GlobalUsings.cs`): domain → `NodaTime` + the service's own `Entities`/`ValueObjects`/`Enums`/`Errors` (`D2.Shared.Time` is NOT globalized — `IClock` is reached via a per-file alias `using IClock = D2.Shared.Time.IClock;` to avoid the ambiguity between `NodaTime.IClock` and `D2.Shared.Time.IClock` that a global `D2.Shared.Time` using would create); app → `D2.Shared.Handler`; tests → `Xunit` + `AwesomeAssertions`. **SA1200 exemption**: `GlobalUsings.cs` files are exempt from SA1200 (inside-namespace placement) via a `[**/GlobalUsings.cs]` section in `.editorconfig` — `global using` directives are top-level by C# language rule and cannot be nested inside a namespace. **Never globalize** (the safety set): `Microsoft.EntityFrameworkCore*`, `Microsoft.Extensions.DependencyInjection*`, `Microsoft.Extensions.Options*`, `Microsoft.Extensions.Logging*`/`Serilog*`, `Microsoft.AspNetCore.*`, `Microsoft.Extensions.Hosting`, `System.Security.Cryptography`, any vendor SDK. **WHY never-globalize the safety set:** a global `using` for EF/Options/DI/logging would blind the compiler-enforced dependency-direction law — a domain file could silently take an EF or Options dependency. Keeping them explicit means a file's usings still reveal what it is coupled to, and the dependency law stays compiler-enforced.

### The module-within-host carve-out

A service-shaped module embedded in a host (KeyCustodian and the auth module both live inside Edge) takes the standard `domain/` + `app/` + `infra/` unchanged but **omits `api/` and its own `tests/`**: the host's `api/` is the composition root (the module exposes `AddD2<Module>()` as its seam), the host's api does the module's transport mapping, and the module's tests live in the host's test project under a `<Module>/` subtree. The carve-out is explicit and named so it never silently becomes the default — the five-project shape is the default. A module is promoted to a full standalone service the moment it needs an independent deployable, an independent database lifecycle, or independent scaling; at that point it gains its own `api/` + `tests/` and `domain/`/`app/`/`infra/` carry over unchanged.

### Clients

A client library is a standalone consumer-facing package that references **contracts + shared libs only** — never the service's `domain/`/`app/`/`infra/` internals. **WHY:** a consumer depends on the client to call the service across a process boundary; if the client pulled in the service's internals, every consumer would transitively compile those internals (and their vendor SDKs), defeating the point of a thin RPC client. The client's detailed internal layout is left to a focused future amendment; the contracts-and-shared-libs-only boundary is the hard constraint that holds today.

## Consequences

**Positive.**

- One predictable shape across every service: a contributor who learns one service's layout finds every other service's layout structurally identical. "Where is the test for `RotateKeyHandler`?" / "where does the proto mapper live?" / "where is the rotation rule?" each answer themselves.
- The domain stays rich and pure: all no-port, no-IO logic lives in `Rules/`, unit-testable with zero infrastructure, and the dependency-direction law keeps EF/Options/DI out of the domain at compile time.
- The dependency law is compiler-enforced, not just documented: the never-globalize safety set means a domain file taking an EF or Options dependency is a visible explicit `using`, and the api-is-the-only-Infra-referrer rule means a layering violation fails the build.
- Mapper placement is unambiguous: the five-surface rule names exactly one home per surface, ending the "mappers had three undocumented homes" drift.
- Vendor swaps and additions are localized: the mandatory vendor subfolder means a second vendor drops into a sibling folder with nothing else moving, and the keyed-resolver pattern handles runtime multi-vendor selection with a typed failure on an unknown key.
- The module-within-host carve-out is principled and reversible: a module shares the host's composition root and test suite until it earns its own deployable, with a named promotion trigger.

**Negative / trade-offs.**

- More nesting than a flat layout: the mandatory vendor subfolder adds a level even for a sole implementation, and the two-section app split plus per-operation folders trade flatness for predictability. The trade is deliberate — cross-service consistency outweighs the marginal nesting cost.
- The persistence mapper living in app (not infra) surprises engineers expecting all persistence concerns in the infra layer. The EF-as-DDD framing (app speaks the query language; infra owns the database) resolves the discomfort but must be learned. See [ADR-0017](0017-ef-as-ddd-persistence.md).
- Adopting the standard across existing services is a cross-layer refactor: namespace moves cascade through every handler `using`, the DI extension, and the test project. The dependency law and per-operation co-location reduce the *future* cost of every change, but the one-time migration is real.

## Alternatives considered

- **Leave service structure undocumented ("the host/service shape is out of scope").** Rejected — it is exactly what produced the drift in Context (anemic domains, three-way provider naming, undocumented mapper homes, flat `Models/` buckets). An explicit, written standard is the whole point.
- **Keep the letter-coded `C/Q/U/X` handler tiers + the `Interfaces/` ⇄ `Implementations/` mirror.** Rejected — the single-letter category folders collide visually with the CRUD `C/R/U/D` letters, the mirror split puts an interface and its impl several folders apart, and the `U/` utility tier and `X/` complex tier had no crisp membership test. Replaced by two full-word categories, per-operation co-location, and the binary side-effect rule.
- **A third "Complex" handler category** for retrieval-named operations that mutate. Rejected — it has no crisp boundary against `Commands/` (both mutate); the binary side-effect rule makes the third tier unnecessary (a retrieval-named op with a write side effect is just a `Command`).
- **A generic `Providers/` wrapper** for vendor adapters. Rejected — it produced the three-way provider-naming mess; capability concern + mandatory vendor subfolder is unambiguous and is the seam a second vendor lands on.
- **A flat `Models/` DTO bucket.** Rejected — a flat folder of records has no per-shape owner; co-location by operation (or promotion to a domain VO when a shape is shared) gives every DTO exactly one owner.
- **Drop the `.App`/`.Infra` namespace segment via `RootNamespace`.** Rejected — in a service the layer is semantics; collapsing the segment saves one path component but forces every reader to memorize a folder-vs-namespace mismatch.
- **Globalize EF / DI / Options / logging usings repo-wide** to cut per-file noise. Rejected — it would blind the compiler-enforced dependency-direction law (a domain file could silently take an EF dependency). Only universal-and-signal-free namespaces (`D2Result`, the utilities surface, `TK`) globalize; the dependency-revealing ones stay explicit on purpose.

## References

- [ADR-0017](0017-ef-as-ddd-persistence.md) — EF-as-DDD persistence: the `DbContext` contract + flat record + pure mapper this ADR physically places (`app/Infrastructure/Persistence/`), and the "App speaks the query language, Infra owns the database" framing behind the persistence-mapper-in-app rule.
- [ADR-0016](0016-keycustodian-lifecycle-store.md) — the KeyCustodian sum-type lifecycle whose layout is the first realization of this standard.
- [ADR-0018](0018-spec-driven-error-codes.md) / [ADR-0019](0019-wrapped-result-wire-model.md) — the spec-driven error codes + wrapped-result wire model the generated `Errors/` namespace and the api transport surface carry.
- [`docs/PATTERNS.md`](../PATTERNS.md) — the daily-reference operational form of this standard (the folder shapes, `Commands`/`Queries`, the five-surface mapper rule, the keyed-resolver recipe).
- [`docs/dev/rules.md`](../dev/rules.md) §5 / §7 / §9 — the auditable predicates derived from this decision.

## Future direction (not part of this decision)

A unified per-service wire-contract toolchain is a noted direction, **not a commitment here**: one bespoke per-service contract spec that references the shared cross-service catalogs (error codes, geo, headers, encryption-frame, field-constraints) and, through D2-owned emitters, generates a service's whole wire surface from a single file — the DTO types (one shape serializing to both the gRPC message and the REST JSON body), the gRPC server stub, the REST endpoint (route + idempotency + resiliency), and the typed client method (with wrapped-result decode). The case for it is that D2 already owns the emitter layer (the error-codes and geo source-gens prove it) and that the two-protocol + per-endpoint-policy + single-DTO model is awkward to bolt onto raw proto. Its hard design prerequisite is a real transport surface to design against — the Edge's first REST + gRPC surface — and TypeSpec is the off-the-shelf hedge to evaluate first. It is sequenced as its own platform deliverable; it does not change any rule in this ADR.

<!--
Copyright (c) DCSV. All rights reserved.
-->


> **Visibility: PUBLIC** — ships with the open surface (`public/`).  
> Do not add product IP, private paths, or non-exportable runbooks.
# ADR-0021: Unified operation-contract IDL — one source per operation generates every representation across three transport planes, with TypeSpec as the compiler front-end and a D2-owned emitter fleet

- **Status**: Accepted (validated 2026-06-13 by the supervised TypeSpec spike)
- **Date**: 2026-06-13 (service-auth cross-ref: 2026-06-18; establishment cross-ref: 2026-06-30; one-gRPC-service-per-op amendment: 2026-07-02)
- **Deliverable**: Edge contract-IDL spike

## Context

D²-WORX already runs the spec-to-codegen pattern seven times — error codes, request context, headers, geo data, scopes, audiences, and JWT claims each derive a typed artifact in two languages from one declarative source. The context spec is the closest precedent: one `*.spec.json` produces `IRequestContext.g.cs` and `IRequestContext.g.ts`, so the C# and TypeScript runtimes can never disagree on the shape. That pattern stops at *types*. It has never reached *operations* — the endpoints, their per-hop transport bindings, and the security policy attached to each.

The next surface to build needs exactly that reach. An operation has to be authored once and produce every representation it touches: the C# and TypeScript DTOs (records with correct nullability and `[RedactData]` on PII; `T | undefined` on the TS side), the `.proto` messages and services, the per-version OpenAPI document, the public REST route registration with its policy applied, the internal gRPC service base and typed client, the SSE binding for server push, the in-process module leaf interface, the handler interface stub, the policy-metadata table, and the parity tests that keep all of those honest. The same operation definition has to serve **three transport planes**:

- **External edge** — a browser or BFF speaks REST/JSON (and SSE for push) to the Edge.
- **Internal service** — service talks to service over gRPC, cross-process (Edge to a backend, or A to B).
- **In-process** — one module calls another inside a single host (the auth module calling KeyCustodian inside Edge), with no serialization at all.

This is **platform-wide, not edge-specific**. Every service, the Edge, and the BFF speak the same contract language, and each operation contract is **owned by the service that owns the capability** — authored once, consumed everywhere. The surface happens to land first at the Edge because the Edge is the first multi-module host and the external ingress, but nothing in the design is edge-only.

An earlier investigation explored **proto-as-the-spine**: define services and messages in `.proto`, lean on gRPC JSON transcoding for the public REST surface, and carry policy on custom proto options. That direction does not survive contact with the requirements. Three structural walls stood out, each confirmed by dedicated research:

- **Proto cannot carry per-route policy out of the box.** `google.api.http` transcoding binds *routing only* — verb and path. There is no mechanism to attach required scopes, a rate-limit tier, a risk tier, or an audience to an individual transcoded route. Custom `MethodOptions` can *hold* the data, but a custom buf plugin (in Go or TypeScript, since the C# protobuf runtime does not expose the option internals) has to read them and emit the enforcement wiring. Policy ends up a bolt-on, not a first-class trait.
- **C# type fidelity is structurally wrong.** Grpc.Tools generates mutable partial classes that implement `IMessage<T>` — not the immutable `record` DTOs the codebase uses, with no `#nullable enable` annotations and no place for `[RedactData]`. Bridging that needs a custom plugin or post-generation patching either way.
- **SSE is not proto's shape.** Transcoding maps a server-streaming method to line-delimited JSON (ndjson), never `text/event-stream` with `data:`/`event:` framing. The SSE binding is hand-written regardless of the IDL.

The net of that investigation: proto-first does not *reduce* the codegen work — it relocates which file is hand-authored while leaving the same volume of bespoke generation. The conclusion inverts the spine: **proto is one of the generated outputs, not the source.**

Two forces then dominate the actual decision. **Policy must be first-class in the contract** — scopes, rate-limit tier, risk tier, audience, idempotency posture declared *on the operation* and emitted to both runtime enforcement and the OpenAPI docs, so the declaration cannot drift from the enforcement. And the **dual REST+gRPC binding from one operation** is the load-bearing risk: a single op has to produce both a REST route (with path/query parameters) and a gRPC method (a flat message) even when the two shapes diverge, without the source bifurcating into two parallel definitions. If that binding does not hold from one source, the single-source-of-truth guarantee collapses at the most important seam.

## Decision

**Adopt TypeSpec as the compiler / type-system / decorator / language-server front-end, and write a D²-owned emitter fleet on top of it. Proto and OpenAPI are generated outputs, not sources. Humans author exactly two things — the `.tsp` contract and the handler bodies — and everything else is emitted.**

### Why TypeSpec is the front-end, not the whole solution

The expensive, hard-to-get-right part of an IDL is the **compiler front-end**: cross-file type resolution, generics, nullability, union/enum/map handling, cycle detection, actionable diagnostics, and a language server for editor tooling. That is precisely what hand-rolled IDLs accumulate debt against and what no team of this size should build. TypeSpec's stable core supplies all of it under MIT license: the type/model/operation/interface system, the decorator + `stateMap` metadata mechanism, the stable OpenAPI 3.0/3.1 and JSON Schema emitters, the custom-emitter API, and the VS Code language server. Its tooling is Node.js + TypeScript — the runtime the existing codegen already lives in — so no new language enters the build pipeline.

What TypeSpec does *not* supply for this codebase is just as clear, and it is all the D²-specific output: the `D2Result`-wrapped wire model, `[RedactData]` on PII, the `BaseHandler`/`I<Op>Handler` pattern, the in-process leaf interface, the policy-enforcement wiring, and the parity tests. TypeSpec's own first-party protobuf, C# server, and SSE emitters are all preview-stage and architecturally mismatched (the C# server emitter generates plain ASP.NET controllers; the protobuf emitter drops optional-scalar presence and has no `oneof`). **None of those preview emitters is load-bearing here** — the decision adopts TypeSpec for the type system and the decorator engine, and owns every code-generating emitter. That decoupling is what neutralizes the preview-emitter risk: a preview emitter that breaks or stalls is one this codebase never depended on.

### The D² emitter fleet — seven emitters

A `@d2/typespec-emitters` package contains the emitter fleet, each a `$onEmit` function that walks the compiled TypeSpec program graph and produces D²-shaped output:

| Emitter | Produces |
| ------- | -------- |
| C# DTO | `record` types with correct nullability and `[RedactData]` on PII properties |
| TS DTO | TypeScript types with `T?` (optional shorthand — never `T \| null`) |
| proto | `.proto` messages + services, fed to buf / Grpc.Tools for the gRPC wire |
| OpenAPI (D² extension layer) | per-version OpenAPI with the `x-d2-*` policy extensions the stock emitter cannot surface |
| route + policy | .NET REST route registrations with scope / tier / risk / audience enforcement applied |
| in-process leaf | `I<Module>Api` + `<Module>Api` façade pair — curated public interface (in `Client` namespace) + sealed delegating impl (in `app/` namespace), for in-host module-to-module calls. The leaf is the call site for the `InProcessModule` context-establishment boundary ([ADR-0025](0025-request-context-establishment.md)): before dispatching, it marks the request's `Origin` as a genuine in-process module call, distinct from a cross-process hop merely dispatched through in-process code. |
| parity | the cross-language and registry-existence validation tests (see below) |

The stock `@typespec/openapi3` emitter validates the HTTP shape correctly and is used for that; the D² OpenAPI extension layer adds the policy extensions on top.

### The dual-binding convention — proven, not assumed

The load-bearing risk is settled by a concrete, validated convention: **the proto emitter reads `op.parameters` (the raw model graph); the REST emitter reads `getHttpOperation` (the resolved HTTP binding). Both views reference the same model node** — `@typespec/http` does **not** clone or mutate the body model when it resolves the HTTP binding, even when a `@path` parameter is mixed with the `@body`. "Flattening" only re-buckets *parameter references* (a path param moves to its own bucket); it never touches the `@body` model. So one operation carries both a REST route and a gRPC method, the proto emitter recovers the full flat message from `op.parameters` independent of any HTTP path/query splitting, and the source never bifurcates. This is the convention every future op author inherits, and it is verified against real running code (see Validation).

> **Amendment (2026-07-02): one gRPC service per operation.** The proto/gRPC-service emitter produces one `.proto` file and one C# service class **per operation** decorated with `@d2GrpcMethod` (`<Service>_<method>.g.proto` + `<Service>Service.g.cs`; `emitter.ts` item 5/6). Two operations that share a `@d2GrpcMethod` service name therefore collide — two protos declaring the same `service` in one package, and the same C# service class emitted twice. The convention every op author inherits is: **each gRPC-bound operation gets its own gRPC service, named for the capability it serves** (for example `KeyCustodianCertificateAuthority` for leaf issuance and `KeyCustodianCaCertificate` for the CA-certificate fetch — two ops, two services). This aligns with the per-service scope-enforcement model the host wires (`MapGrpcService<T>().RequireAnyScope(...)`): operations with different required scopes belong on different services, because a shared service would force both behind one route policy. Grouping multiple ops under one proto `service` (the natural proto idiom) is not supported and is not the model.

### The `@d2*` decorator vocabulary — policy as first-class contract data

Policy is declared on the operation through a stable D² decorator library (the `@d2/typespec-decorators` package), each decorator writing to a `program.stateMap` keyed by a stable `Symbol.for("D2.<dec>")` so every emitter can read it back across the package boundary:

- `@d2Scope` — the required scope (referencing the scopes registry).
- `@d2RateLimitTier` — the rate-limit bucket.
- `@d2Audience` — the token audience.
- `@d2ServedBy` — the owning service/module (drives forwarding; see below).
- `@d2GrpcMethod` — the gRPC service + method binding marker (the dual-binding companion to the HTTP decorators).
- `@d2Redact` — marks a property as PII (drives `[RedactData]` emission), applied at the `ModelProperty` level.

One declaration, read by every emitter: a scope becomes an `x-d2-scope` OpenAPI extension *and* a `RequireAnyScope` attribute on the generated route *and* a row in the policy-metadata table *and* an assertion in a parity test. The declaration and its enforcement cannot drift because they share a source.

### Declarative gateway / forwarding — location transparency

An operation declares its **owner** via `@d2ServedBy` plus its external binding and policy, and the toolchain generates the Edge-side wiring: a REST route that enforces auth/scope and then **forwards to the owner** — a generated **gRPC client** when the owner is a separate service, or the in-process **leaf** when the owner is a module inside the Edge — with **zero hand-written passthrough for a pure proxy**. Edge handler code is hand-written only where the Edge adds real logic (aggregate, transform, orchestrate, or its own module's operation). The contract names the owner ("served by KeyCustodian"); whether that resolves to an in-host leaf call today or a cross-process gRPC hop tomorrow is a generated/deployment concern that swaps **without touching the contract**.

### The named, non-growing hand-written fringe

The toolchain does not force generation. A small, named, non-growing set of endpoints stays hand-written and documented: form-encoded (`/oauth/token`), multipart upload, the SSE *binding* itself (the ndjson-vs-`text/event-stream` gap is universal), and any arbitrary-shape endpoint. These are roughly five to ten endpoints that do not grow with the surface. The guardrail holds throughout: **generate the contract surface, hand-write the handler body** — a pure proxy writes zero handler; a rich handler maps its generated input DTO to a domain aggregate at its boundary (`Domain.Create(input)`), which is where validation lives and is meaningful app work, not plumbing.

> **Amendment (2026-06-28): well-known JSON (JWKS + OIDC discovery) is GENERATED, not fringe.** The JWKS document and the OIDC discovery document are served from generated `@route @get @d2Harmless` operations on `key-custodian.tsp` — the route registration, the DTOs, and the in-process leaf façade all come out of the emitter fleet, and the OIDC document's canonical snake_case keys are produced by the standard `@encodedName("application/json", …)` → `[JsonPropertyName]` emitter path. They were struck from the fringe list above because the assumption that they could not be generated was never tested: the existing `health` fixture already proved the `@get @d2Harmless` decorator stack emits a routed anonymous endpoint, so JWKS + OIDC discovery were generable all along. **General rule going forward: a "fringe / hand-written / in-process-only" classification requires a TESTED capability gap — an op that actually fails to emit — not an assumed one.** The remaining fringe (form-encoded, multipart, the SSE binding, arbitrary-shape) is retained because each has a real, demonstrated emitter gap.

### Existing sub-specs compose; they are not replaced

The operation IDL *references* the existing spec-driven sources — an op's error set references the error-codes spec, its scope references the scopes/audiences source-gen, and the request-context spec remains the ambient enrichment channel. The op-IDL composes with them; it does not re-declare them.

### Parity / validation tests are a first-class output

The parity emitter generates the "it cannot silently drift" guarantees — every op's declared scope exists in the scopes registry; every referenced error code exists in the relevant error-codes spec; the C# and TS emit of every message agree on field set, optionality, and casing; a REST route and a gRPC method bound to the same op carry identical input/output types; no duplicate `(verb, path, version)`; every generated `I<Op>Handler` resolves to a registered implementation; the declared audience and tier are known values. These continuous checks are a primary justification for the whole system.

## Consequences

**Positive.**

- **One source per operation, drift-proof by construction.** Every representation — DTOs in both languages, proto, OpenAPI, REST routes, gRPC services, SSE bindings, the in-process leaf, policy tables — derives from one `.tsp` contract. The classes of drift the seven existing spec pipelines already eliminate for types now extend to operations, bindings, and policy.
- **Policy is first-class, not a bolt-on.** Scopes, tier, risk, audience, and idempotency are declared on the op and emitted to both runtime enforcement and the docs from the same declaration, so an endpoint's stated policy and its enforced policy cannot diverge.
- **Parity tests are a built output.** Scope existence, cross-language DTO parity, binding consistency, route uniqueness, and handler presence are generated assertions, not manual vigilance — the contract is continuously verified rather than trusted.
- **Forwarding is location-transparent.** A pure-proxy edge route is generated with zero hand-written passthrough, and the leaf-vs-gRPC resolution swaps under the contract as a module is extracted into its own service — the contract does not change when deployment topology does.
- **The compiler front-end is borrowed, the emitters are owned.** The expensive infrastructure (type resolution, generics, cycle detection, diagnostics, the language server) comes from a stable, MIT-licensed, actively maintained foundation; the team writes emitters in TypeScript — its demonstrated strength — and inherits OpenAPI and JSON Schema emission for free.
- **Broad coverage from one toolchain.** The single-source model covers roughly 85% of the wire surface (the public REST + internal gRPC bulk), with the fringe named and bounded.

**Negative / trade-offs.**

- **This is the largest codegen system in the repo.** Seven emitters plus a decorator library plus the parity-test generator is a substantial, bounded body of work that the team owns and maintains. The maintenance burden compounds as the contract surface grows — every new IDL feature must be handled by each emitter.
- **A new IDL language enters the repository.** TypeSpec `.tsp` is a real language contributors must learn, and contract definitions live in TypeSpec while the implementations live in C# — a split a `.NET`-primary engineer must get used to. The tooling runtime is Node.js, which the codebase already runs, so no new *runtime* enters the pipeline, but the *language* does.
- **Build cost.** A compile-and-emit step is added to the build; the TypeSpec compiler is an ESM package with top-level `await` (it must be loaded via dynamic `import()`, never `require()`), so the surrounding tooling is `.mjs`/ESM.
- **TypeSpec's first-party proto / C# / SSE emitters are preview.** This is **mitigated by owning every emitter** — the decision depends on TypeSpec for the type system and decorator engine only, so a preview emitter that breaks or stalls is one this codebase never relied on; the residual exposure is to the *stable* core, whose abandonment risk is low (Microsoft runs Azure's own API specs on it).
- **The fringe stays hand-written.** Form-encoded, multipart, the SSE binding, and arbitrary-shape endpoints remain hand-authored and documented — named and non-growing, but real. (Well-known JSON — JWKS + OIDC discovery — was removed from this fringe: it is generated; see the amendment under "The named, non-growing hand-written fringe".)
- **Carry-forward build conventions are mandatory, learned from the spike.** The decorator library must ship a dedicated `tsp-index.js` that defines `$decorators`, kept separate from the package `main` (the emitter API) — importing the package `main` from the `.tsp` double-loads the decorator implementations and breaks `using` with an `ambiguous-symbol` error. The error model must enumerate concrete status codes (`400 | 401 | …`) because `@statusCode` rejects a bare `int32`. State keys must be stable `Symbol.for("D2.<dec>")` exports so `stateMap(KEY).get(op)` works across the package boundary.

## Validation

The decision is validated by a supervised spike, not reasoned in the abstract. Against **TypeSpec 1.13.0** (with `@typespec/http` and `@typespec/openapi3` 1.13.0), a real prototype was compiled and emitted for the KeyCustodian `Sign` operation — an internal-only, in-process, gRPC-also-bound op with a non-trivial `bytes`-in / compact-string-out shape, chosen precisely because it stresses the auth chain, the leaf path, and the dual binding at once.

All six success criteria passed:

- **The dual-binding kill-switch did not fire.** A custom emitter obtained both `op.parameters` and `getHttpOperation(program, op)` for the `sign` op and proved `rawBodyModel === httpBodyModel` is `true` — the `SignInput` body model is the *referentially identical* node in both views. An adversarial control confirmed the `===` test discriminates (it is `false` against the return type and against an empty object), and the harder `@path`-mixed-with-`@body` case still held (`true`). The companion-interface fallback is **not** required; the dual REST+gRPC binding holds on one op.
- **The `@d2*` decorators read back.** Every op-level decorator (`@d2Scope`, `@d2GrpcMethod`, `@d2ServedBy`, `@d2Audience`, `@d2RateLimitTier`) and the property-level `@d2Redact` round-tripped via `program.stateMap(KEY).get(...)`.
- **OpenAPI emitted correctly.** The stock emitter produced `POST /internal/v1/kc/sign` with the `SignInput` body, the `SignOutput` 200, a `D2ErrorResponse` on every error status, and `bytes` rendered as `{ "type": "string", "format": "byte" }` — confirming `bytes` coerces only in the schema projection, never in the model graph the proto emitter reads.
- **The parity gate works as designed.** The spike declared a workload scope present in the private product scopes catalog and confirmed the parity emitter's `reportDiagnostic` path by using an out-of-registry scope as the adversarial control — demonstrating the scope-existence gate live.

The seed `@d2/typespec-decorators` package and a diagnostic emitter were authored as part of the spike and proved the `extern dec` + JS-impl + `stateMap` pattern on a real compile. The two now-proven build conventions (the dedicated `tsp-index.js` split; the stable `Symbol.for` state keys) are carried into the production emitter fleet as hard requirements.

## Alternatives considered

- **Proto-as-the-source.** Define services and messages in `.proto`, transcode to REST, carry policy on custom options. Rejected, and the rejection is what inverts the spine. Policy is routing-only out of the box and needs a custom Go/TypeScript buf plugin to reach enforcement; generated C# is mutable partial classes with no NRT and no `record` shape and no place for `[RedactData]`; OpenAPI on .NET 10 is blocked by an unresolved `Microsoft.OpenApi.Models` namespace collision between `Grpc.Swagger` and the built-in OpenAPI package; SSE is ndjson, never `text/event-stream`; and cross-spec references to the scopes/error-code registries carry no compile-time validation. The bespoke bridge required is not smaller than adopting a richer IDL. Proto stays a generated **output**, where its 85%-coverage gRPC/REST strength is real.

- **Smithy (AWS).** A mature IDL with the cleanest trait system in the field. Rejected on toolchain and coverage. There is no official protobuf/gRPC emitter (the request has been open since 2022); the .NET codegen is a single-maintainer community plugin (5 GitHub stars) that generates none of the codebase's patterns; SSE has no protocol binding anywhere in the ecosystem; and the entire codegen framework runs on the JVM with Java/Kotlin emitters — a foreign toolchain for a Node/.NET shop. Adopting Smithy means writing four or five custom Java emitters plus a JVM/Gradle dependency, with custom work comparable to a bespoke build but in an unfamiliar language.

- **OpenAPI-first (Kiota / NSwag / openapi-generator / Fern).** Rejected at the design level. OpenAPI is a REST documentation format, not a multi-transport IDL: it cannot describe gRPC services, SSE semantics, the in-process leaf, or transport-invariant bindings. The generators are client-focused (Kiota, Fern, Stainless emit clients only; NSwag and openapi-generator scaffold controllers that match none of the codebase's conventions), and `x-` policy extensions never reach runtime enforcement — no toolchain bridges `x-d2-scope` to ASP.NET middleware. Using any as the source would force a separate hand-authored `.proto`, breaking single-source-of-truth. They may remain useful downstream as consumers of the *emitted* OpenAPI.

- **A bespoke IDL built from scratch.** Rejected as the highest-cost, lowest-value path for a team this size. The emitters are the team's strength and would be written either way; the *compiler front-end* — cross-file type resolution, generics, cycle detection, versioning, diagnostics, and a language server — is the universal regret and the long-tail time sink across every org that has built one (Stripe, AWS, Palantir, Microsoft all confirm they would not rebuild the front-end given an adoptable engine). A thin JSON op-spec consistent with the existing pipelines was weighed and breaks at the first cross-file type reference, which is the first 20% of a real compiler. Adopting TypeSpec means choosing to write emitters (the strength) rather than a compiler (not the strength), against a type graph that is correct-by-construction.

## References

- [ADR-0019](0019-wrapped-result-wire-model.md) — the wrapped-result wire model whose `D2ResultProto` envelope and `D2ErrorResponse` arm the generated wire DTOs carry; the emitters target this codec rather than re-inventing one.
- [ADR-0020](0020-service-project-structure.md) — the service-project structure standard whose layer boundaries the generated artifacts respect: transport mappers land in `api/Mappers/`, the api is the composition root that binds generated routes/gRPC services, and a module-within-host exposes the generated leaf as its seam. This ADR realizes the unified wire-contract direction that ADR-0020 noted as a future direction without committing to an engine.
- [ADR-0022](0022-service-auth-mint-once-forward.md) — the mint-once-at-the-Edge, forward-the-token-unchanged service-auth model this contract IDL is the codegen vehicle for: `@d2ServedBy` drives the generated forwarding client (a cross-process gRPC client or an in-process leaf) that forwards the once-minted token unchanged, `@d2Audience` for internal operations resolves to the single broad internal audience `d2.internal`, and the build-time caller-scopes-superset-of-callee-scopes check and the propagated call-path field are additive emitter outputs the forward model relies on.
- [ADR-0023](0023-mtls-workload-identity.md) — the mTLS workload-identity decision: the generated cross-process gRPC client this IDL emits runs over the mutually-authenticated TLS channel ADR-0023 secures, so the channel carrying the forwarded token between service processes is authenticated and encrypted.
- [ADR-0018](0018-spec-driven-error-codes.md) — the spec-driven error codes the op-IDL references rather than re-declares; the error-code-existence parity test validates the reference.
- [ADR-0007](0007-request-context-propagation.md) — the request-context spec that remains the ambient enrichment channel an op composes with, distinct from the per-op contract.
- [ADR-0025](0025-request-context-establishment.md) — the `InProcessModule` context-establishment boundary the generated in-process leaf calls before dispatching a module-to-module call, and the broader establishment model (`Origin` / `ImmediateCaller` / `CallPath`) that closes the confused-deputy gap the plain in-process/cross-process distinction left open.
- [ADR-0002](0002-spec-driven-codegen.md) — the spec-driven-codegen precedent this generalizes from "one spec → a type in two languages" to "one source → types + operations + bindings + policy across every language and transport."

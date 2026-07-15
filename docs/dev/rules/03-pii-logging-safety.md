<!--
Copyright (c) DCSV. All rights reserved.
-->

## 3. PII / Logging Safety
<a name="top"></a>
_[← rules index](../rules.md) · §3 of the D2-WORX rules catalog._

**Predicate index:** §3.1–§3.17 · 17 predicates.

User input, message bodies, presigned URLs, AMQP URIs, broker passwords, IPs, emails, addresses, names, file metadata, fingerprints — all PII. The biggest leak vector in this codebase is logging exception messages (broker libs embed credentials in their `ex.Message`).

### What counts as PII (non-exhaustive)

- Email addresses (any form, even hashed if reversible)
- Phone numbers
- IP addresses (v4 + v6)
- Geographic / location data (lat/long, addresses, city+postal beyond country)
- Names (first, last, display, business)
- User-generated content (messages, posts, descriptions, comments)
- File names, file content, file metadata
- Presigned URLs (contain credentials in query params)
- AMQP / DB / Redis connection strings (contain passwords)
- JWT tokens, session tokens, API keys, refresh tokens
- Browser fingerprints, device fingerprints
- Authentication state (user IDs in some contexts)
- Audit trail content (who did what when)

### Predicates — §3 PII / logging safety

- **3.1** Does any `[LoggerMessage]` partial-method declaration in this scope accept `Exception` as a parameter? (Y = bug; the log sink will format `ex.ToString()` and leak `ex.Message`.)
  - Evidence: `grep -rEn '\[LoggerMessage' <scope>` → for each hit, inspect parameter list → confirm none take `Exception`.
  - **Real-example leak**: `BrokerUnreachableException.Message` embeds the AMQP URI INCLUDING the password. Same class: `OperationInterruptedException.Message` from RabbitMQ.Client includes broker-side text (PRECONDITION_FAILED arg dumps); `RedisException.Message` from StackExchange.Redis can include connection-string fragments + command details; `Npgsql.PostgresException.Message` includes row data in constraint-violation messages; ANY user-handler exception in a subscriber-isolation `catch` carries arbitrary user-input.
  - **No per-call-site carve-out.** The receiving log delegate signature IS the contract; future callers will pass real exceptions even if today's call site synthesizes a controlled one (e.g. `new InvalidOperationException(result.ErrorCode ?? "unknown")`). The next refactor that adds an inline-catch site through the same delegate inherits the leak risk for free.
  - **Carve-out** (rare, must be pinned by a contract test): `ContinueWith`-only fault sinks where the handled exception path NEVER sees user-input-derived content (e.g. unobserved background-task faults). When a delegate has BOTH a sanitized inline-catch site AND a fault-sink ContinueWith site, SPLIT it into two delegates with separate names so each shape is independently pinned — never share.
  - **Canonical enforcement**: a reflection-based contract test (`LeakProneLogDelegates` `TheoryData` listing every leak-prone delegate + per-delegate carve-out `[Fact]`s for the explicit fault-sink exceptions). See `tests/Unit/Messaging/Telemetry/LoggerMessageDelegateContractTests.cs` for the pattern.

- **3.2** Does any `try/catch` log `ex.Message` directly (string interpolation, structured field, format arg) without going through a sanitized renderer (e.g. `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)`)?
  - Evidence: `grep -rEn 'ex\.Message\|exception\.Message' <scope>` → per hit, classify safe/unsafe.
  - **Pattern**: emit `TypeName` + `FirstFrame` as separate structured fields; never the raw `Message`.

- **3.3** Does any logged structured field carry user input (email, IP, address, message body, AMQP URI, presigned URL, file name) without `[RedactData]` on its declaring type?
  - Evidence: per logged field → type declaration → `[RedactData]` check.
  - **How**: `[RedactData]` lives on the type, applies to ALL Serilog logging recursively, reflection-cached. Don't reach for per-handler RedactionSpec when `[RedactData]` does the job.

- **3.4** Does any AMQP message header carry sensitive context plaintext (instead of being inside the encrypted `ContextEnvelope` payload)? (Brokers store headers as plaintext at-rest.)
  - Evidence: per `IBasicProperties.Headers` write → classify field as routing-only (W3C `traceparent`/`tracestate`) or context.
  - **Sensitive context → encrypted envelope.** Routing tracing → plaintext header.

- **3.5** Does any OTel span attribute or metric tag carry PII?
  - Evidence: per `Activity.SetTag` / `Counter.Add(..., new TagList { ... })` → classify each tag value.
  - **Allowed**: outcome (`success` / `fail` / `not_found`), code-path identifiers, error categories. **Forbidden**: emails, IPs, names, message bodies.

- **3.6** Does any test fixture log a raw exception via a code path that production also reaches?
  - Evidence: walk test fixtures touching `ILogger` → confirm no production code path shares the same logging sink.

- **3.7** Are user-input strings (HTTP body, query params, JWT claim values, message payloads) length-validated upstream of any storage / log emission?
  - Evidence: per user-input boundary → length-validation call site (e.g. `MAX_AUDIENCE_LENGTH` checks, `_MAX_JWT_PAYLOAD_SEGMENT_LENGTH` cap on token-exchange).

- **3.8** Does sign-out / logout clear ALL auth state (server session via Auth, in-memory JWT invalidation, SvelteKit `invalidateAll()`, Redis session entries, cookie cache)?
  - Evidence: per sign-out path → trace each surface.

- **3.9** Are API key / secret / token comparisons constant-time (`CryptographicOperations.FixedTimeEquals`)? Plain `==` is a timing-attack vector.
  - Evidence: per `apiKey == ` / `secret == ` / `token == ` → switch to `FixedTimeEquals`.

- **3.10** Does client-side telemetry (Faro user identity) include only `userId` + `username`? No email, real name, contact details?
  - Evidence: per Faro identity setter → fields confirmed.

- **3.11** Was `Grep` run against `secrets/` or `.env.secrets` by name? (Behavioral rule — never grep these.)
  - Evidence: tool-call history check → expect no hits.
  - **If a secret accidentally enters context** (runtime output, grep match), STOP and tell the operator immediately so they can rotate the exposed value.

- **3.12** When a presigned URL is generated, does it have the shortest reasonable TTL? (Long TTLs become long-lived secrets.)
  - Evidence: per presigned URL generation → TTL value + justification.

- **3.13** Are tokens / secrets / fingerprints in flight always encrypted (TLS) and at rest in cache layers always treated as sensitive (encrypted in Redis where possible; ephemeral TTL otherwise)?
  - Evidence: per cache write of token → encryption + TTL confirmed.

- **3.14** Do log AND error / exception messages include enough context to debug WITHOUT including PII? (e.g. log `userId` hash + outcome instead of `email`; error text `JWKS fetch failed for issuer=<issuer>; status=<httpStatus>` — issuer is config, not PII.) (Canonical message-context gate — §20.4 is the DX cross-pointer.)
  - Evidence: per log / error message in scope → information sufficiency vs PII trade-off documented.

- **3.15** Is at-rest PII anonymization (GDPR right-to-erasure) implemented via `D2.Shared.DataGovernance` (`[Anonymizable]` / fluent `.Anonymize*` → `D2:Anonymize` annotation + `IAnonymizationEngine`) rather than ad-hoc NULL-wipes or hard-deletes?
  - **Faux / tombstone values** are non-i18n developer-supplied literals. Anonymization (at-rest overwrite) is **strictly separate** from `[RedactData]` (Serilog log-masking) — decoration is independent in both directions; the startup guard does NOT cross-check `[RedactData]` completeness.
  - **Decoration is opt-in** per field; a forgotten PII field is the consumer's responsibility, not a boot failure.
  - **`AnonymizationModelValidator` (deny-by-default)** enforces declared-rule integrity and fails host startup with a PII-safe message before traffic. Validated rules: ownership marker + `IAnonymizationTrackable` on decorated entities; Tier A/B only (no Tier C); template sibling existence; attribute-without-convention; divergent attribute+fluent double-declaration; `SetNull` only on nullable columns.
  - **Engine logging omits the subject id** (logs a fresh per-sweep `sweepId` + counts + model-metadata names only; never a raw or hashed user/org id).
  - Evidence: per entity carrying PII fields → entity type + field decoration confirmed via `[Anonymizable]` or fluent `.Anonymize*`; engine is wired through `IAnonymizationEngine.AnonymizeUserAsync`/`AnonymizeOrgAsync`; no raw `null`-assignments or hard-delete paths for erasure.
  - **Why**: ad-hoc NULL-wipes are undiscoverable (no startup guard, no idempotency, no Tier classification, no test-coverage gate) and hard-deletes lose the audit trail GDPR Art. 17 requires; `D2.Shared.DataGovernance` makes erasure observable, testable, and startup-guarded. The `[RedactData]` vocabulary separation is intentional — log-masking hides PII from telemetry at runtime, anonymization overwrites PII at rest on erasure; conflating them causes consumers to skip one layer believing the other covers it.
  - **How**: decorate each PII field with `[Anonymizable(<kind>)]` (attribute) or `.Anonymize*(...)` (fluent) at model-build time. Register via `services.AddD2DataGovernance(...)` + `builder.Services.AddDbContext<TContext>(...)` so the `AnonymizationModelValidator` runs at startup. Call `IAnonymizationEngine.AnonymizeUserAsync(userId)` / `AnonymizeOrgAsync(orgId)` on subject erasure.

- **3.16** When a live credential (a raw bearer token forwarded byte-for-byte to a downstream hop, a captured API key, any secret retained in request-scoped state for later use) is wrapped in a value type, is that type's complete never-logged guarantee proven by a LEAK-SURFACE MATRIX covering every vector — not one `ToString()`-redacts assertion?
  - **Scope**: any hand-built wrapper value type (`readonly struct` / `record`) that carries a live secret in request-scoped state for forwarding or later reuse — distinct from `[RedactData]`-decorated PII data types (which §3.3 governs) because the raw bytes must remain reachable through a single explicit reveal seam. Out of scope: a secret that is consumed immediately and never held in a typed wrapper (no leak surface to matrix).
  - **The required shape**: the raw secret lives in a PRIVATE field reachable ONLY via one explicit reveal method (`.RevealForX()`); `ToString()` / `IFormattable` / serialization all return a constant placeholder; the type carries `[RedactData]`; the type is structurally ISOLATED from the enrichment / log-OK projection (it is NEVER a field on the request-context type that the logging pipeline walks); it is NEVER a `[LoggerMessage]` parameter.
  - **The matrix (every vector pinned, not a subset)**: (a) `ToString()` returns the placeholder (not the secret); (b) the secret value is EXCLUDED from the request-context / enrichment field-set — a structural test asserts the log-OK projection AND the context contract contain no field of the wrapper type; (c) log-capture across a real capture→reveal→forward cycle shows the placeholder, never the secret, in the rendered output; (d) a reflection scan asserts no `[LoggerMessage]` partial-method takes the wrapper as a parameter (the §1.6 sibling for this type); (e) a live-tree source scan pins the SOLE production caller of the reveal seam (the one credential-attach site) — non-vacuously guarded, test files excluded.
  - **Evidence**: per live-credential wrapper introduced in scope → one test file:line per matrix vector (a)-(e); the private-field + single-reveal-seam shape confirmed by reading the type; the structural-isolation test naming the field-set it walks. A wrapper proven only by a `ToString()`-redacts assertion (vector (a) alone) = FINDING-HIGH (the other four vectors are each an independent leak path that (a) does not cover).
  - **Why**: a never-logged guarantee is only as strong as its WEAKEST vector. A wrapper that redacts `ToString()` but (i) exposes the secret through a public property the `{@x}` destructuring operator walks, (ii) sits on the request-context type every enricher projects, (iii) is passed to a `[LoggerMessage]` delegate, or (iv) has its reveal seam called from an unexpected re-logging site — leaks despite the redaction. The empirical `ForwardedJwt` leak-path-exhaustion table enumerated 18 distinct vectors; a single redaction assertion would have rubber-stamped a wrapper that leaked through any of the other seventeen. This is the §3 analogue of the 0022 mTLS log-contract reflection-guard shape.
  - **How**: ship the full matrix in the SAME step — one behavior-descriptive test per vector (§1.8), the structural-isolation test asserting the enrichment field-set excludes the wrapper type, the reflection no-`[LoggerMessage]`-param scan, and the live-tree sole-reveal-caller scan. At PLAN time the §1.22 adversarial-coverage matrix enumerates the leak vectors as the auth/secret category. Cross-ref §3.1 / §1.6, §3.3, §1.18, §9.39.
  - *Provenance: deliverable 0023 Step 2 — the `ForwardedJwt` redacting wrapper; the leak-path-exhaustion table enumerated 18 vectors, and a single `ToString()`-redacts assertion would have left seventeen unproven.*

- **3.17** Does every log-redaction marker (`[RedactData]` on a KEEP data type, the codegen `@d2Redact` decorator on a spec field) declare an ACCURATE `RedactReason` data-classification naming the field's TRUE class — with the codegen emitter REQUIRING the reason (fail-loud on a bare marker) rather than silently defaulting to `PersonalInformation`, so secret-adjacent material is never misclassified as PII?
  - **The rule**: `RedactReason` is a mandatory argument on the `@d2Redact` decorator; the emit path errors out on a marker carrying no reason instead of emitting a default. Secret-adjacent material (raw signing input, key bytes, token fragments) names `SecretInformation`; genuine personal data names `PersonalInformation`; each marker names its own true class. The mask fires the same either way — the reason exists to route the value into the correct data-governance regime, not to decide whether the value is masked.
  - **Evidence**: `private/contracts/typespec/key-custodian/key-custodian.tsp` — `signingInput` (secret-adjacent) carries a `SecretInformation` reason, not the PII default; the `@d2Redact` decorator definition + the `csharp-dto-emitter.ts` emit path require the reason and fail-loud on its absence; the `RedactReason` enum lives in `D2.Shared.Utilities`. A bare marker the emitter accepts by defaulting to `PersonalInformation` = FINDING-HIGH.
  - **Why**: the mask fires regardless of the reason, but the reason drives data-governance routing — and the PII and secret regimes are STRICTLY SEPARATE (§3.15). A hard-coded `PersonalInformation` default silently mislabels every future secret-adjacent field as personal data, routing it through the PII / GDPR-erasure path instead of secret-handling. Fail-loud forces the author to state the true class at declaration time.
  - **How**: keep the reason a mandatory decorator argument; the emitter throws on a bare marker (never defaults). Each marker names its true class — `SecretInformation` for secret-adjacent material, `PersonalInformation` for personal data. Cross-ref §3.15, §3.3, §3.16.

<sup>[↑ jump to top](#top)</sup>

---


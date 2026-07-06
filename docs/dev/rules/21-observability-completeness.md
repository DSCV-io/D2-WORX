<!--
Copyright (c) DCSV. All rights reserved.
-->

## 21. Observability Completeness
<a name="top"></a>
_[← rules index](../rules.md) · §21 of the D2-WORX rules catalog._

<!-- VERBATIM-BEGIN -->

Production code that you can't observe is production code you can't debug, can't optimize, and can't trust. Every operation must emit traces + metrics + logs at the right granularity.

### Predicates — §21 observability completeness

- **21.1** Does every handler emit an OTel span via `BaseHandler` (auto)?
  - Evidence: per handler → span emission confirmed.

- **21.2** Do all logs and spans include the universal correlation fields (per §7.9 table)?
  - `traceId`, `correlationId`, `userId`, `orgId`, `service`.
  - Evidence: per log/span site → fields present.

- **21.3** Does every counter / gauge / histogram have a documented unit + meaningful tag set?
  - Evidence: per `Meter.Create*` → unit + tags documented.

- **21.4** Are span attributes structured (key-value pairs), not concatenated strings?
  - Evidence: per `Activity.SetTag` → key-value form.

- **21.5** Is every long-running operation (> 1s typical) instrumented with a histogram for duration?
  - Evidence: per long-running op → histogram present.

- **21.6** Are error counters tagged with outcome categories (e.g., `outcome=fetch_success`, `outcome=fetch_fail_network`, `outcome=fetch_fail_validation`)?
  - Evidence: per error counter → outcome taxonomy.

- **21.7** Are debug logs sufficient to reconstruct a failed request's flow without reproducing it?
  - Evidence: per non-trivial flow → log-trace audit.

- **21.8** Are health checks comprehensive (DB connectivity, broker connectivity, downstream service availability, key custodian readiness)?
  - Evidence: per service → health check enumeration.

- **21.9** Are slow-operation thresholds set per handler (via `HandlerOptions`)?
  - Evidence: per latency-sensitive handler → threshold value.

- **21.10** When a spec catalog enumerates a closed set of telemetry / wire-identifier tag VALUES (not just tag NAMES) — e.g. an OTel messaging activity tag whose value is constrained to `publish` / `consume` / `process`, an outcome-category enum constrained to `success` / `fail_network` / `fail_validation`, a span-status enum, a metric-tag closed list — does the scope ship runtime-emission pin tests asserting each enumerated value is ACTUALLY emitted by at least one production code path?
  - **Evidence**: per spec-catalog value → pin test asserting the value reaches the production telemetry surface (e.g. listener-fixture capturing the emitted activity tag + asserting the value matches the catalog entry). For each catalog entry: `<catalog.X = "value">` → `<test file:line that emits and observes "value">`.
  - **Why**: spec-driven catalog NAMES are pinned at the wire boundary by parity tests + catalog-pin guards (§1.21) — drift in the tag NAME is caught structurally. Tag VALUES are a SECOND drift surface that the name-level pins do not cover: a call site can emit a value not in the spec's enumeration (silent over-emission), OR the spec can enumerate a value no code path produces (silent under-emission). Neither failure mode breaks a build or surfaces in pure unit tests — both ship as production observability gaps that take a real incident or downstream alerting failure to detect. Empirical cite: deliverable 0007 Step 5 spec-driven the OTel messaging activity tag NAMES — eliminating the active `messaging.operation` vs `messaging.operation.type` consumer-publisher semconv drift bug at the name level. But the VALUES that those tags carry (`publish` / `consume` etc.) remained call-site-emitted; without runtime-emission pin tests, a refactor that renames a publisher's emission from `"publish"` to `"send"` would NOT fail any name-level test — the wrong value would silently flow to OTel, breaking downstream dashboards / alerting rules that filter on the catalog value. Cluster E's final-review recommendation surfaced this as the next-tier discipline: spec-driven catalog NAMES close one drift surface, runtime-emission pins close the other.
  - **How**: when introducing a closed-set value catalog for telemetry, ship a corresponding `Emit_<ValueName>_IsActuallyEmittedBySomeCodePath` test per catalog entry — or a single table-driven `[Theory]` with one `[InlineData]` row per entry. The test exercises the production code path that should emit the value (via TestHost / in-memory `ActivityListener` / `MeterListener` capture), then asserts the captured tag carries the spec value verbatim. For pure-publisher-side catalogs (the code emits to OTel; no downstream code reads back), the test fixture captures via `ActivityListener.ActivityStarted` / `ActivityStopped` callbacks. For consumer-side catalogs (the code reads an incoming activity tag), the test injects a span carrying the catalog value + asserts the consumer's behavior. Pair with the structural §1.21 catalog-pin guard (catches name drift) — together the two predicates close both drift surfaces.
  - **When**: applies to any spec catalog enumerating closed-set VALUES (telemetry tag values, span-status enums, metric-tag closed lists, wire-identifier enums consumed by both emitter + observer). Does NOT apply to open-set values (e.g. user-id tags, free-form metadata fields). Does NOT apply to spec catalogs that enumerate only NAMES (those are covered by §1.21 structural guards + per-VALUE pin tests per §1.18).

- **21.11** Does every telemetry tag KEY (e.g. `"capability"`, `"reason"`), closed-set tag VALUE (reason codes like `"minter-required"`, capability names, sentinels like `"<none>"`) that a hand-authored metric or log emits live as a NAMED CONSTANT (single source of truth), referenced at EVERY use site (the emitting `.Add(...)`, any `switch` producing it, any `==`/`is` comparison against it), rather than a raw string literal duplicated across those sites?
  - **The rule**: a bounded / closed set of observability strings is defined ONCE as named `const string`s (UPPER_CASE per §7), co-located with the counter / logger definition, or via the codebase's tag-constant convention (the spec-generated `AuthTelemetryTags.g.cs` shape for spec-driven meters). Hand-authored, non-spec-driven meters STILL name their closed sets. One wire-contract pin test protects the values.
  - **Evidence**: `server/services/edge/key-custodian/app/Application/Observability/KeyCustodianMetrics.cs` `AuthorityRejections` (`TAG_CAPABILITY` / `TAG_REASON` + nested `Reason` / `Capability` / `Workload` consts); `SignHandler.DenyWithTelemetry` + `JwtSigningCapability.DenyWithTelemetry` reference them; `AuthorityTelemetryTests.AuthorityRejectionTags_PinWireContract` pins the wire values; the spec-driven analog is `AuthTelemetryTags.g.cs`.
  - **Why**: a closed set of magic strings scattered across a `switch` + a `==` compare + a `.Add()` + prose silently rots on a typo — `reason == "minter-required"` against a bare literal is the classic footgun (a typo makes the branch quietly dead with NO compile error). Named constants give one source of truth plus a single pin test. "It's just a bounded metric-tag value, the emitted-constant rule does not apply" is not a license for scattered literals.
  - **How**: define the closed set as constants (or a small nested static class per enum); reference the constants at every emit / switch / compare site; add one wire-contract pin test. BOUNDARY: this requires NAMED constants, NOT generated ones — do not over-engineer a hand-authored meter into a spec / codegen pipeline just to satisfy it. Cross-ref §26.21 (the spec-emitted-constant analog: §26.21 covers values a generator owns; this covers hand-authored closed sets no generator owns), §5 (naming), §21 (observability completeness).

<sup>[↑ jump to top](#top)</sup>

---


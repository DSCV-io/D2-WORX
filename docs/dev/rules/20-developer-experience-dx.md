<!--
Copyright (c) DCSV. All rights reserved.
-->

## 20. Developer Experience (DX)
<a name="top"></a>
_[← rules index](../rules.md) · §20 of the D2-WORX rules catalog._

**Predicate index:** §20.1–§20.14 · 14 predicates.

Code that future engineers (including future-you) can read, debug, extend, and refactor without reverse-engineering. Sensible defaults. Ergonomic call sites. No footguns.

### Predicates — §20 developer experience (DX)

- **20.1** Are sensible defaults provided for every Options record, so call sites can be `new()` for "I'll take the defaults"?
  - **Pattern**: §5.13 (nullable-param ctor + `?? default`).
  - Evidence: per Options record → defaults hold up to inspection.

- **20.2** Are footguns absent from public API surface? (No methods that look right but silently do the wrong thing — e.g., accepting `string?` and treating null as a special sentinel without documenting.)
  - Evidence: per public API → footgun audit.

- **20.3** Are call sites concise? `client.ExchangeAsync(subjectToken, ct)` beats `client.ExchangeAsync(new TokenExchangeOptions { ... }, ct)` when defaults work.
  - Evidence: per public method → call-site readability.

- **20.4** Error / exception-message debuggability facet (enough context to debug, no PII) — consolidated into §3.14 (canonical message-context gate); ID retained for citation stability.
  - Evidence: walk §3.14.

- **20.5** Are exceptions thrown only for truly exceptional conditions (system invariant violations, programmer errors), not for control flow? (`D2Result` is the control-flow type.)
  - Evidence: per `throw` → exceptional-condition justification.

- **20.6** Do public APIs follow the principle of least surprise? (Given the type signature, what does a developer expect? Does the implementation deliver that?)
  - Evidence: per public API → "would I be surprised?" check.

- **20.7** Are dependency injections explicit (constructor params), not service-locator style (`provider.GetService<T>()` mid-method)?
  - Evidence: per service resolution → injection point.

- **20.8** Are interfaces minimal (Interface Segregation Principle — small, focused interfaces beat fat ones)?
  - Evidence: per new interface → method count + coherence.

- **20.9** Do tests serve as documentation (clear names, focused assertions, "given-when-then" structure where helpful)?
  - Evidence: per test → readability check.

- **20.10** DX / IntelliSense-hover facet of XML-doc quality — consolidated into §11.17 (canonical xmldoc-quality gate); ID retained for citation stability.
  - Evidence: walk §11.17.

- **20.11** Is debug logging available at appropriate verbosity? (Production logs at INFO; DEBUG / TRACE available for troubleshooting.)
  - Evidence: per non-trivial flow → logging coverage.

- **20.12** Are sensible config defaults documented in the README so an onboarding engineer can run the service without reading the source?
  - Evidence: per service README → defaults documented.

- **20.13** Are common debugging scenarios covered in the README (how to inspect DLQ, how to read Tempo / Loki traces, how to query the audit log)?
  - Evidence: per service README → ops docs.

- **20.14** Are public extension methods discoverable (named clearly, namespaced consistently, not hidden in obscure namespaces)?
  - Evidence: per new extension → namespace + name.

<sup>[↑ jump to top](#top)</sup>

---


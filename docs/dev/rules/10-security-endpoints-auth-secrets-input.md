<!--
Copyright (c) DCSV. All rights reserved.
-->

## 10. Security (Endpoints / Auth / Secrets / Input)
<a name="top"></a>
_[← rules index](../rules.md) · §10 of the D2-WORX rules catalog._

**Predicate index:** §10.1–§10.24 · 24 predicates.

Security predicates recur and need explicit checking. **D²-WORX is being built to ship to production with real users; security predicates are non-negotiable.**

### Predicates — §10 security

- **10.1** Do all list queries enforce pagination limits (default 50, max 100)?
  - Evidence: per list query → limit cap confirmed.

- **10.2** Are PG constraint errors caught and mapped to appropriate HTTP status (PG `23505` → 409 Conflict, not 500)?
  - Evidence: per PG-touching path → catch + mapping confirmed.

- **10.3** Is auth middleware visible at the route declaration (`.RequireAuth()`, `.RequireServiceKey()`, `.RequireOrg()`)? Not implicit.
  - Evidence: per new route → middleware decoration visible.

- **10.4** Are new JWT custom claims namespaced with `d2_` (snake_case — `act["d2_kind"]`, `d2_session_id`)? Documented in `docs/JWT-CLAIMS.md` (when published)?
  - Evidence: per new claim → prefix + doc.

- **10.5** Are sensitive IDs absent from JWT (admin user IDs, internal audit data stays server-side / session only)?
  - Evidence: per JWT mint → claim audit.

- **10.6** Does auth middleware fail-closed on missing config (missing JWKS / issuer / liveness config or missing secrets = 401 immediately, never silently bypass)?
  - Evidence: per auth middleware → fail-closed branch confirmed.

- **10.7** Does sign-out clear ALL auth state? (Cross-ref §3.8.)

- **10.8** Constant-time API-key / token / secret comparisons. See §3.9 for the canonical predicate. This row is the §10 cross-pointer; walk §3.9 for evidence.

- **10.9** Is multi-instance migration safety enforced? (Cross-ref §9.9.)

- **10.10** Are SQL queries parameterized (no string concatenation building SQL)?
  - Evidence: per `dbContext.FromSqlRaw` / `db.Database.ExecuteSqlRaw` → parameterization confirmed.

- **10.11** Is user-rendered HTML escaped / sanitized (XSS prevention)? (Svelte handles this natively for `{interpolated}` content; raw HTML via `{@html ...}` requires explicit sanitization.)
  - Evidence: per `{@html}` use → sanitizer call.

- **10.12** Are CSRF protections in place for state-mutating browser forms? (Built-in to SvelteKit form actions when used correctly; bypassed if you call mutating APIs directly via fetch with credentials but no CSRF token.)
  - Evidence: per state-mutating endpoint → CSRF strategy.

- **10.13** Does rate limiting protect every public endpoint (per the rate-limit tier system in [`docs/v2/PHASE_3_RATE_LIMITING.md`](../v2/PHASE_3_RATE_LIMITING.md))?
  - Evidence: per new endpoint → rate-limit tier assignment.

- **10.14** Are session cookies `HttpOnly` + `Secure` + `SameSite=Strict` (or `Lax` if cross-site links needed)?
  - Evidence: per cookie set → flag check.

- **10.15** Are uploaded files scanned for malware (via ClamAV / equivalent) before persistent storage?
  - Evidence: per upload path → scan step confirmed.

- **10.16** Are file-type validations done by content-sniffing (magic bytes), not just extension or `Content-Type` header (which are user-controlled)?
  - Evidence: per upload validation → content-sniffing confirmed.

- **10.17** Does session rotation happen on auth-state change (login, sign-out, password change, MFA enrollment)?
  - Evidence: per auth-state-change handler → session rotation confirmed.

- **10.18** Is JWT signing key rotation supported via the JWKS overlap pattern (old key valid during overlap window after new key published)?
  - Evidence: per key rotation flow → overlap window confirmed.

- **10.19** Are user passwords never logged, never sent in error messages, never persisted in session state?
  - Evidence: per password-touching code → secrecy audit.

- **10.20** Does the codebase NEVER log JWTs / API keys / OAuth tokens (even truncated)?
  - Evidence: per token-handling code → log audit.

- **10.21** Are URL parameters validated as untrusted? (Path traversal `../` blocked; ID-shaped params parsed via TryParseTruthyNull.)
  - Evidence: per URL param read → validation step.

- **10.22** Are admin / staff actions audit-logged with userId + targetId + action + timestamp + outcome?
  - Evidence: per admin/staff action → audit log entry.

- **10.23** On multi-listen hosts, is product gRPC (and other crown-jewel / internal-only RPC) structurally isolated to the mTLS listen role — never registered on the shared endpoint table for cleartext HTTP or Issuer-HTTPS-without-client-cert binds?
  - **The rule**: a host that binds more than one listen role (e.g. cleartext health, public Issuer HTTPS, mTLS internal gRPC) MUST Map product gRPC only on the mTLS port/role (`MapWhen` on `Connection.LocalPort` / equivalent). Internal services (Audit, later Files, …) use cleartext/HTTP **only** for infrastructure (health / alive / metrics); all product gRPC is mTLS-only. JWT scopes remain required; bind isolation is defense-in-depth so dual-factor is not “right port luck.”
  - **Evidence**: per multi-bind host → product `MapGrpcService` / internal RPC Maps appear only under the mTLS branch; public binds carry health/well-known/bridges only. Edge KC gRPC → mTLS `:9443`; Audit Ping gRPC → mTLS only. A crown-jewel Map on Issuer `:8443` or cleartext `:8080` = FINDING-HIGH.
  - **Why**: shared Maps on multi-bind Kestrel expose crown-jewel RPC to public/Issuer planes for probing and amplify any future authority regression. 0030 D-SEC-02/03.
  - **How**: `MapWhen(LocalPort == MTLS_PORT)` (or separate app/pipeline) for product gRPC; keep well-known/health on public binds. Cross-ref §10.24, §9.42, §9.41.

- **10.24** After request-origin establishment on product gRPC, does the platform fail-closed when `RequestOrigin` is still `Unestablished` — without relying on each handler to remember the check?
  - **The rule**: non-Harmless gRPC product methods deny when Origin remains `Unestablished` after the cross-process establishment interceptor (order: JWT auth → Origin establish from mTLS peer → Unestablished deny). Harmless methods skip. Handler-level Origin checks are not the primary wall.
  - **Evidence**: `RequestOriginUnestablishedDenyInterceptor` registered after `RequestOriginCrossProcessInterceptor` via `AddD2RequestOriginGrpc`; `AUTH_REQUEST_ORIGIN_UNESTABLISHED`; tests pin all four RPC shapes + A2B no-peer path. Missing platform deny with only handler checks = FINDING-HIGH for internal product surfaces.
  - **Why**: establishment without deny leaves JWT-only single-factor on a wrong bind; platform deny is uniform. Cross-ref §9.42, §9.41, §10.23.

<sup>[↑ jump to top](#top)</sup>

---


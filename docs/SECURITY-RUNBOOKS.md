<!--
Copyright (c) DCSV. All rights reserved.
-->

# SECURITY-RUNBOOKS.md — Compromise Response Runbooks

> **Status**: Placeholder. Filled in detail when KeyCustodian ships with the Edge auth module.
>
> **Until then**: this doc declares intent. The runbooks themselves are stub bullet points to be expanded with concrete commands, alert criteria, and recovery steps.

---

## Scope

Runbooks for compromise scenarios across D²-WORX's KeyCustodian-managed secrets:

- **Auth JWKS signing keys** (RS256 keypair, 90-day rotation)
- **Message payload encryption keys** (AES-256-GCM per encryption-domain, quarterly rotation)
- **Cookie signing secret** (90-day rotation)
- **Service-identity OAuth `client_secret`s** (180-day rotation)
- **Root key** (encrypts all keys at rest in `auth_db`, 1-2 year manual rotation)

Plus third-party API keys (Twilio, Resend, IPinfo) — manual rotation per provider's documented process.

---

## Common Pattern: Emergency Rotation CLI

```bash
d2 keys rotate --domain <audit|notifications|courier|jwks|cookie> --reason "<short-description>" --emergency
```

**What `--emergency` does** (KeyCustodian state machine semantics):
1. Marks current active kid as `compromised` (terminal state — cannot be promoted back to active)
2. Generates + activates a new kid (no smoke-test delay — immediate)
3. Force-emits `d2.security.key-rotated` event with `urgent=true`
4. Consumers reload immediately on event receipt; old key drops from active keyring
5. Background job scans last N days of audit events for messages encrypted with the compromised kid; flags for forensic review
6. Auto-generates incident report row in `audit_db.incident` with full timeline

---

## Runbook Stubs (to be expanded when KeyCustodian ships)

### TBD: Message-payload key compromise (audit / notifications / courier domain)

Steps:
- TBD: detection criteria (alert thresholds, logging signals)
- TBD: full CLI invocation + expected output
- TBD: forensic review query patterns
- TBD: customer notification policy (if applicable)
- TBD: post-incident retrospective template

### TBD: JWT signing key compromise

Steps:
- TBD: detection criteria
- TBD: emergency rotation CLI (likely `d2 keys rotate --domain jwks --emergency`)
- TBD: invalidate all active sessions (clear Redis session cache)
- TBD: force global re-auth (announcement to users)
- TBD: emit `d2.security.signing-key-compromised` event for downstream awareness
- TBD: forensic review

### TBD: Cookie signing secret compromise

Steps:
- TBD: detection
- TBD: rotation
- TBD: cookie invalidation propagation (relies on session revocation in Redis)

### TBD: Service-identity OAuth client_secret compromise

Steps:
- TBD: detection
- TBD: per-client rotation
- TBD: deploy coordination (consuming service needs the new secret before rotation completes)

### TBD: Root key compromise (worst case)

Steps:
- TBD: detection (this should be EXTREMELY rare given the root key never lives in env files; mounted from `secrets/auth/root.key`)
- TBD: full re-encryption procedure (decrypt all `encryption_key.key_material_encrypted` rows with old root, re-encrypt with new root, atomic transaction)
- TBD: stop-the-world coordination (likely requires brief downtime)
- TBD: post-rotation validation

### TBD: Third-party API key compromise

Steps:
- TBD: provider-specific rotation steps (Twilio / Resend / IPinfo)
- TBD: update `.env.secrets` + restart affected services
- TBD: forensic review of API audit logs (if provider supports)

---

## When This Doc Goes from Placeholder to Real

Completion criteria (when KeyCustodian ships):
- [ ] All 6 stub runbooks above expanded with concrete commands, alert criteria, recovery steps
- [ ] Each runbook tested in a dev/staging environment (operator walks through it end-to-end)
- [ ] Operator on-call rotation knows where to find this doc
- [ ] Alert routing references runbook sections (alert → page → "see SECURITY-RUNBOOKS.md §X")

Until then: anyone who hits a compromise scenario should:
1. Pause and consult the on-call security contact
2. Don't wing the rotation — KeyCustodian state machine has terminal states; incorrect operation can lock you out
3. Document what you did so this runbook can be informed by it

---

## Related references

Inbound auth runtime — JWT validation, JWKS handling, session liveness, transport-binding wiring (consult these when a compromise touches the inbound boundary):

- [`server/shared/dotnet/auth/README.md`](../server/shared/dotnet/auth/README.md) — `D2.Shared.Auth` (composition root, JWKS provider, JWT validator, session liveness tracker, telemetry, debugging table for `AUTH_*` failure codes).
- [`server/shared/dotnet/auth-http/README.md`](../server/shared/dotnet/auth-http/README.md) — `D2.Shared.Auth.Http` (HTTP transport binding: `JwtAuthMiddleware`, `RequireD2Scope` / `[D2RequireScope]`, RFC 7807 ProblemDetails shape).
- [`server/shared/dotnet/auth-grpc/README.md`](../server/shared/dotnet/auth-grpc/README.md) — `D2.Shared.Auth.Grpc` (gRPC transport binding: `JwtAuthInterceptor`, scope attributes, `RpcException` trailer shape).

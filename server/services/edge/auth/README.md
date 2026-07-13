<!--
Copyright (c) DCSV. All rights reserved.
-->

# Edge Auth module

> Parent: [`../README.md`](../README.md)

> **Status: NOT IMPLEMENTED** — design keep: [docs/v2/PHASE_3_AUTH_CORE.md](../../../../docs/v2/PHASE_3_AUTH_CORE.md) · JWT/anon/KC: [PHASE_3_AUTH.md](../../../../docs/v2/PHASE_3_AUTH.md) · spine: [PHASE_3.md](../../../../docs/v2/PHASE_3.md)

Module-within-host (`domain` / `app` / `infra`); composition on `D2.Edge.Api`; database `d2-auth`.

**Build order (locked):** **Auth Core → Minting → Auth Extras**. Do not ship mint without Core domain/storage.

**Auth Core design surface** (see keep L9–L163 — not a skinny stub): user lifecycle SM; multi credential methods (+ org IdP/SCIM **full law** §13); challenges; 3-tier sessions (elevate/sign-out, no-org); org trees + downward proxy + membership hot/history; **root org lifecycle** (Active/Frozen/Banned/PendingClosure/Closed); invitations (in-app accept); security policy floor+user+org; platform sub entitlements (flag→entitlement→scope); `oauth_client`; sign-in attempts; dual-audit outbox; retention/redaction fanout. **Minting (A3)** embosses claims from those facts. UI polish may trail; architecture does not.

**Design audit:** [PHASE_3_AUTH_CORE_DESIGN_AUDIT.md](../../../../docs/v2/PHASE_3_AUTH_CORE_DESIGN_AUDIT.md) (remediated). **Keep open until** O23 rate limiting + O24 fingerprint discussions.

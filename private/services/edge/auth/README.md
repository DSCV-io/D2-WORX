<!--
Copyright (c) DCSV. All rights reserved.
-->

# Edge Auth module

> Parent: [`../README.md`](../README.md)

> **Status: NOT IMPLEMENTED** — design keep: [private/docs/v2/PHASE_3_AUTH_CORE.md](../../../../private/docs/v2/PHASE_3_AUTH_CORE.md) · JWT/anon/KC: [PHASE_3_AUTH.md](../../../../private/docs/v2/PHASE_3_AUTH.md) · spine: [PHASE_3.md](../../../../private/docs/v2/PHASE_3.md)

Module-within-host (`domain` / `app` / `infra`); composition on `DcsvIo.D2.Private.Edge.Api`; database `d2-auth`.

**Build order (locked):** **Auth Core → Minting → Auth Extras**. Do not ship mint without Core domain/storage.

**Auth Core design surface** (see keep L9–L186 — not a skinny stub): user lifecycle SM; multi credential methods (+ org IdP/SCIM **full law** §13); challenges; 3-tier sessions (**id-rotate elevate**, hard yeet vs soft re-mint, idle/absolute timeouts, no-org); org trees + downward proxy + membership hot/history; **root org lifecycle** (Active/Frozen/Banned/PendingClosure/Closed); invitations (in-app accept; seat reserve + accept re-check); security policy floor+user+org; platform sub entitlements (flag→entitlement→scope; constrained no-plan); `oauth_client`; sign-in attempts; dual-audit outbox; retention/redaction fanout. **Minting (A3)** embosses claims from those facts. UI polish may trail; architecture does not. Product SKUs/onboarding = private wip only.

**Design review (full set):** start [PHASE_3_AUTH_CORE.md §0](../../../../private/docs/v2/PHASE_3_AUTH_CORE.md). Audit trail: [PHASE_3_AUTH_CORE_DESIGN_AUDIT.md](../../../../private/docs/v2/PHASE_3_AUTH_CORE_DESIGN_AUDIT.md); Fable report: [PHASE_3_AUTH_CORE_FABLE_AUDIT.md](../../../../private/docs/v2/PHASE_3_AUTH_CORE_FABLE_AUDIT.md). Rate limit / fingerprint: [PHASE_3_FINGERPRINTING.md](../../../../private/docs/v2/PHASE_3_FINGERPRINTING.md) then [PHASE_3_RATE_LIMITING.md](../../../../private/docs/v2/PHASE_3_RATE_LIMITING.md).

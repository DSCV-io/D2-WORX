<!--
Copyright (c) DCSV. All rights reserved.
-->

# 0026 — KeyCustodian complete crypto surface + consumer surface (symmetric + sealed)

Ship snapshot. Full working record: `docs/wip/0026-kc-crypto-surface/` (gitignored). Branch `n/kc-crypto-surface`.

## What shipped

KeyCustodian's complete crypto surface — signing, symmetric payload keys, per-service sealing keypairs, leaf issuance, CA distribution, JWKS + OIDC discovery — and its complete consumer surface (keyring distribution + sealed seal/open libs), .NET and TypeScript, proven in isolation (Testcontainers + in-memory gRPC TestServer + Testcontainer RabbitMQ + faithful doubles).

Headline capabilities:

- **CA root-key structural isolation (§9.44)** — every stored-root plaintext touch (sign + the lifecycle smoke checks) routes through one dedicated `ICaRootSigningCapability` in a single composition root, with a structural deny on the general surface, a DI-isolation test, and one instrumented chokepoint (EventIds 9518/9519). The general surface cannot resolve it.
- **Sealed-domain messaging auto-wire** — spec-driven per-domain `mode`/`consumerService`; the composer seals/opens by consumer identity; one-call `AddD2SealedEncryptionViaKeyCustodian` (opener wired only where the spec names you consumer); a producer host structurally holds no opener.
- **Type-witnessed auto-encrypting publisher (TS)** — publishing plaintext on an encrypted domain is a compile error; unwired encrypted domains fail loud; a runtime default-deny second lock.
- **One-way sealed domains** — audit/notifications/courier left the symmetric payload catalog (the symmetric machinery preserved via a test seam); three fail-loud nets prevent a downgrade.
- **mTLS workload-identity mesh** — CSR self-issue (leaf SAN = authenticated peer; leaf private key never at KC or on the wire; §9.44-isolated leaf signing), Node + .NET workload-leaf clients, the BFF ruled a mesh-member workload.
- **Client-side keyring caching + push-based rotation** — the per-message encrypt/decrypt/seal/open path is a purely local cache read (zero KC calls); KC is hit only on boot fetch, lazy first-use, and a `key-rotated` fanout event that triggers a hot-swap with 30s grace-zeroize. Built into the shared client on both runtimes (one-call helpers; rotation mandatory).

Cross-runtime crypto parity is byte-pinned (KAT + golden + two-way interop fixtures); ADR-0009 gained the sealed-mode amendment; ADR-0022/0023 the CSR + mesh-member amendments; ADR-0021 finalized.

## Process integrity

Every implementation cycle's per-step audit loop TERMINATED zero-finding (Cycles 1–3). The deliverable-wide FINAL-REVIEW audit loop TERMINATED composed-clean: R1 full K=12 (0 HIGH / 3 MED / 22 LOW) ∪ R2 full K=12 (0 HIGH / 0 MED / 8 LOW) ∪ R3 targeted closure (8/8 by absence, zero new) ∪ one baseline-integrity HIGH remediated. Every finding closed by absence; none carried or reclassified. Gates at ship: build 0/0, inspectcode 0, Edge 1673/0/8, Shared 6799/0, release-runner 479, client-ts 89 @100%, baselines 86/86.

FINAL-REVIEW also hardened the release pipeline: both baseline seeders (.NET RS0016 scrape + TS api-extractor) had a silent-corruption class — a build/extractor no-op wrote an empty baseline and passed the currency gate — now guarded fail-loud with regression tests; the seed↔release-runner fingerprint composition was unified into one shared module and byte-identity-pinned (previously duplicated with four comments falsely claiming a test that did not exist).

## Rule candidates — applied

The predicates distilled from this deliverable were user-approved and APPLIED to `rules.md` at SHIP: 13 new — §1.33 (wall-clock-free test waits), §9.45 (authority-gate-same-step), §9.46 (domain-named authority), §13.1a (commit-marker ceremony), §24.31 (merged-tree final gate), §24.32 (editing agents run inspectcode), §24.33 (persist failing-test names), §24.34 (fix-WPs enumerate every finding ID), §24.35 (verify agent dead before re-dispatch), §24.36 (untracked-aware audit greps), §26.22 (fringe = tested capability gap), §26.23 (seeder fail-loud on empty regeneration), §26.24 (byte-identity-pinned duplicated composition) — plus 4 strengthenings (§24.0h K-level-recorded-before-run, §24.13.1 bounded audit-ID grep, §26.20 ×2 source-content reseed + stage-before-reseed).

## Governing decisions (ADRs)

0009 (async messaging encrypted payloads — sealed mode), 0016/0017 (KeyCustodian lifecycle store, EF-as-DDD), 0020 (service project structure), 0021 (operation-contract IDL), 0022 (mint-once-forward), 0023 (mTLS workload identity — CSR + mesh-member).

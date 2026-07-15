<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/docs/v2

Product phase design home (private monorepo only).

**Active tracking:** [V2.md](V2.md) — architecture + phase map. Layout dual-tree §2 is SoT for repository shape (with [ADR-0026](../../../public/docs/adrs/0026-public-private-monorepo-layout.md) for framework-facing layout law).

## Law

- Never export. Live product tracking and phase docs reside here.
- Operator process law remains under monorepo-root `docs/dev/`, not this tree.
- OSS / Auth Core branch coordination notes live in V2 §2.

<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.App

> Parent: [`../README.md`](../README.md)

Thin **host** App shell for Edge process-level application types. **Not** KeyCustodian App (`key-custodian/app/`).

> **Status:** shell assembly only — no host-level handlers on this assembly. KeyCustodian owns its own App project.

## Purpose

Empty shell for host-module Application + Infrastructure ports (auth, rate-limit, WhoIs, etc.). Modules remain placeholders. Composition root remains **`D2.Edge.Api`** only.

## §11.15 section N/A (shell assembly)

| Section | Status |
| --- | --- |
| Public API | N/A — no public types beyond assembly marker |
| Configuration | N/A — no options; host config lives on Edge.Api |
| Dependencies | N/A — empty shell; see csproj ProjectReferences only |
| Usage | N/A — empty shell; no consumer API on this assembly |
| Telemetry | N/A — no metrics/logs from this assembly |
| Gotchas | N/A — empty shell; do not place composition root code here |

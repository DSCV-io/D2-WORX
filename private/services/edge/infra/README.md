<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Private.Edge.Infra

> Parent: [`../README.md`](../README.md)

Thin **host** Infra shell for Edge process-level adapters. **Not** KeyCustodian Infra (`key-custodian/infra/`).

> **Status:** shell assembly only — no host-level adapters on this assembly. KeyCustodian owns its own Infra project.

## Purpose

Empty shell for host-module vendor adapters. Modules remain placeholders. Only **`DcsvIo.D2.Private.Edge.Api`** may reference this project (ADR-0020).

## §11.15 section N/A (shell assembly)

| Section | Status |
| --- | --- |
| Public API | N/A — no adapters on this assembly |
| Configuration | N/A — no infra Options in this shell |
| Dependencies | N/A — empty shell |
| Usage | N/A — composition root is Edge.Api only |
| Telemetry | N/A — no adapters emit from this assembly |
| Gotchas | N/A — empty shell; no vendor adapters; concern/vendor subfolders stay empty |

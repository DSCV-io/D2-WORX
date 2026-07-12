<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.Domain

> Parent: [`../README.md`](../README.md)

Thin **host** Domain shell for Edge process-level pure domain types. **Not** KeyCustodian domain (`key-custodian/domain/`).

> **Status:** shell assembly only — no host-level domain aggregates on this assembly. KeyCustodian owns its own Domain project.

## Purpose

Empty shell for host-module domain (auth session entities, rate-limit pure math, fingerprint VOs, etc.). Modules remain placeholders. Keeps ADR-0020 layer law ready: Domain ← App ← Infra ← Api.

## §11.15 section N/A (shell assembly)

| Section | Status |
| --- | --- |
| Public API | N/A — no public domain types on this assembly |
| Configuration | N/A — pure domain has no Options |
| Dependencies | N/A — empty shell; no Domain types or shared-primitive deps |
| Usage | N/A — no factories/handlers on this assembly |
| Telemetry | N/A — domain does not emit telemetry |
| Gotchas | N/A — empty shell; no EF / DI / logging in this project |

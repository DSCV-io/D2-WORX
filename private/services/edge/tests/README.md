<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.Tests

> Parent: [`../README.md`](../README.md)

**Who / what:** Contributors and CI — unit + integration tests for Edge host composition and the KeyCustodian module-within-host.

## Layout

| Path | Coverage |
| --- | --- |
| `Unit/Host/` | `AddD2EdgeHost` DI isolation, pipeline order, Map endpoints, three-bind Kestrel, trust anchors, CSR issuer |
| `Unit/KeyCustodian/` | KC domain/app/client unit + TypeSpec/codegen suites |
| `Integration/KeyCustodian/` | KC lifecycle / keyring / seal / mTLS harnesses (Testcontainers where required) |

## Run

```text
dotnet test private/services/edge/tests/D2.Edge.Tests.csproj -- --filter-trait "Category=Unit"
```

Host isolation tests do not start hosted services (outbound leaf refresh issues at host start). Integration suites may require Docker for Testcontainers.

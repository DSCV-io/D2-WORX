<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Tests

> **Status**: placeholder — not yet implemented.

## Purpose

Test infrastructure for ALL `D2.Shared.*` libraries. Deliberately **lumped together** in a single test csproj — overkill to spin up a separate test project for every lightweight shared lib.

## Public API surface (test helpers)

- **Testcontainers spin-up helpers** — one-call helpers to start PG / Redis / RabbitMQ / SeaweedFS / ClamAV in tests. Auto-disposes on test completion.
- **Mock builders** — `D2ResultBuilder`, `JwtClaimsBuilder`, `RequestContextBuilder`, `LocationBuilder`, etc.
- **Redaction-respecting test logger** — captures Serilog output during tests with full `[RedactData]` policy applied (so tests can assert "this log line redacts user.email correctly")
- **FluentAssertions extensions for D2Result** — `.ShouldBeOk()`, `.ShouldBeFailure(statusCode)`, `.ShouldHaveErrorCode(code)`, `.ShouldHaveMessages(...)`, etc.
- **Custom matchers / xUnit fixtures** for handler-pattern tests (BaseHandler invocation + OTel metric assertions)

## Dependencies

- `D2.Shared.Result`, `D2.Shared.Handler`, `D2.Shared.Utilities`, etc. (the libs being tested)
- `xUnit`, `FluentAssertions`, `Moq`
- `Testcontainers` + `Testcontainers.Postgresql` / `Testcontainers.Redis` / `Testcontainers.RabbitMq`

## References

- — `D2.Shared.Tests` deliberately lumped (rationale: per-lib test projects would be overkill for libraries this small)
- [docs/TESTS.md](../../../../docs/TESTS.md) — adversarial test discipline + 8-category Case Coverage Checklist
- — testing strategy + CI lane shape

## Tests for SERVICES go elsewhere

`D2.Shared.Tests` covers the **shared libraries**. Per-service tests live at `server/services/{service}/tests/D2.{Service}.Tests.csproj`. Don't mix them.

<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/typespec-emitters — Emitter Validation Ledger

Byte-parity and structural validation rows for the C# + TS DTO emitters.
Each row maps a committed fixture to its regeneration guarantee.

---

## C# fixture parity table

| Fixture file | Emitter call | Key assertion | Test file | Test name |
|---|---|---|---|---|
| `clients/GetJwksInput.g.cs` (ns `D2.Edge.KeyCustodian.Clients`) | `emitCsharpDtos("getJwks", "D2.Edge.KeyCustodian.Clients", "contracts/typespec/key-custodian/key-custodian.tsp", [], [], [])` | Byte-identical to `GET_JWKS_INPUT_FIXTURE` | `tests/byte-parity.test.ts` | `byteParity_GetJwksInput_CommittedFixtureIdentical` |
| `clients/GetJwksOutput.g.cs` (ns `D2.Edge.KeyCustodian.Clients`) | `emitCsharpDtos("getJwks", "D2.Edge.KeyCustodian.Clients", ..., [], outputFields, [nested("Jwk", ...)])` | Byte-identical to `GET_JWKS_OUTPUT_FIXTURE` | `tests/byte-parity.test.ts` | `byteParity_GetJwksOutput_CommittedFixtureIdentical` |
| `SignInput.g.cs` | `emitCsharpDtos("sign", ..., inputFields, [], [])` | Byte-identical to `SIGN_INPUT_FIXTURE` | `tests/byte-parity.test.ts` | `byteParity_SignInput_CommittedFixtureIdentical` |
| `key_custodian_signer_sign.g.proto` | `emitProto("sign", "KeyCustodianSigner", "Sign", "unary", "d2.keycustodian.v1", "D2.Services.Protos.KeyCustodian.V1", ...)` | Byte-identical to `SIGN_PROTO_FIXTURE` | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_SignProto_CommittedFixtureIdentical` |
| `KeyCustodianSignerService.g.cs` | `emitGrpcService("sign", "KeyCustodianSigner", "Sign", ..., { kind: "facade", typeName: "IKeyCustodianSignerFacade", methodName: "SignAsync", targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade" })` | Byte-identical to `SIGN_SERVICE_FIXTURE` (façade delegation — re-pointed from `ISignHandler` per `@d2InProcess`) | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_KeyCustodianSignerService_FacadeDelegation_CommittedFixtureIdentical` |
| `SignTransportMappers.g.cs` | `emitGrpcService(...)` (mappers file) | Byte-identical to `SIGN_MAPPER_FIXTURE` | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_SignTransportMappers_CommittedFixtureIdentical` |
| `clients/IKeyCustodianInternalApi.g.cs` (ns `D2.Edge.KeyCustodian.Clients`) | `emitFacade("KeyCustodian", [{ opName: "getJwks", ... }], "D2.Edge.KeyCustodian.Clients", "D2.Edge.KeyCustodian.App.Application")` (interface file) | Byte-identical to `INTERFACE_FIXTURE` | `tests/facade-emitter.test.ts` | `facadeEmitter_ByteGate_Interface > regenerated IKeyCustodianInternalApi.g.cs is byte-identical to the committed fixture` |
| `app/Application/KeyCustodianInternalApi.g.cs` (ns `D2.Edge.KeyCustodian.App.Application`) | `emitFacade(...)` (impl file, index 1) | Byte-identical to `IMPL_FIXTURE` | `tests/facade-emitter.test.ts` | `facadeEmitter_ByteGate_Impl > regenerated KeyCustodianInternalApi.g.cs is byte-identical to the committed fixture` |
| `app/Application/KeyCustodianClientsGenerated.g.cs` (ns `D2.Edge.KeyCustodian.App.Application`) | `emitFacade(...)` (DI extension file, index 2) | Byte-identical to `DI_FIXTURE` | `tests/facade-emitter.test.ts` | `facadeEmitter_ByteGate_DiExtension > regenerated KeyCustodianClientsGenerated.g.cs is byte-identical to the committed fixture` |

**Deliberate-drift non-vacuity guard**: each byte-parity describe block contains a second
test that mutates the respective fixture by one byte and asserts the regenerated content
does NOT match (`not.toBe(driftedFixture)`). This proves the byte gate is not a tautology
comparing a buffer to itself — the guard covers `GetJwksInput`, `GetJwksOutput`, and
`SignInput`.

---

## C# structural validation table

| Live type | Generated type | Validated by |
|---|---|---|
| `D2.Edge.KeyCustodian.Clients.GetJwksInput` (parameterless) | `D2.Edge.KeyCustodian.Clients.GetJwksInput` (committed `clients/GetJwksInput.g.cs`) | `GetJwksTransportDtoTests.GetJwksInput_IsParameterless` |
| `D2.Edge.KeyCustodian.Clients.GetJwksOutput` (`IReadOnlyList<Jwk> Keys`) | `D2.Edge.KeyCustodian.Clients.GetJwksOutput` (committed `clients/GetJwksOutput.g.cs`) | `GetJwksTransportDtoTests.GetJwksOutput_HasKeysProperty_OfCorrectType` |
| `D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk` (6 public members) | `D2.Edge.KeyCustodian.Clients.Jwk` (6-field positional record) | `GetJwksTransportDtoTests.Jwk_MatchesDomainVoPublicShape` |
| `[property: RedactData]` on `SignInput.Payload` | `[property: RedactData(Reason = RedactReason.PersonalInformation)]` on generated `SignInput.Payload` | `TypeSpecDtoValidationTests.GeneratedSignInput_PayloadProperty_IsRedactedByRealPolicy` (real Serilog pipeline) |

**Redaction proof**: `GeneratedSignInput_PayloadProperty_IsRedactedByRealPolicy` builds a
real `LoggerConfiguration().Destructure.With<RedactDataDestructuringPolicy>()` pipeline (not a
mock), logs the generated `SignInput` record, and asserts the rendered output contains
`"[REDACTED: PersonalInformation]"` and does NOT contain `"SECRET_PAYLOAD"`.

**Transport-vs-domain-VO divergence note**: the domain `Jwk` VO has 3 positional ctor params + 3
init-only properties with constant defaults (`Kty="RSA"`, `Use="sig"`, `Alg="RS256"`). The
transport `D2.Edge.KeyCustodian.Clients.Jwk` is a 6-field positional record. Equivalence is
validated by public-member-set comparison (6 public members, same names and types), NOT by
constructor arity. The transport DTO lives in `D2.Edge.KeyCustodian.Clients` — a separate project
from the domain VO — because it is consumed by callers who must not depend on the domain layer.

---

## TS fixture parity table

| Shape assertion | Test file | Test name |
|---|---|---|
| `get-jwks-dto.g.ts` contains `export interface GetJwksInput`, `GetJwksOutput`, `Jwk` | `tests/byte-parity.test.ts` | `byteParity_GetJwksDto_TsFile` |
| `readonly kid: string;` inside `Jwk` interface | `tests/byte-parity.test.ts` | `byteParity_GetJwksDto_TsFile` |
| `readonly keys: readonly Jwk[];` inside `GetJwksOutput` | `tests/byte-parity.test.ts` | `byteParity_GetJwksDto_TsFile` |

---

## Cross-language parity

`walkModel` feeds BOTH emitters from the same walk result. Parity is by
construction — both emitters receive identical `fields[]` and `nestedModels[]`.

`tests/dto-parity.test.ts` pins this: one `walkModel` call → C# and TS emit
the same field names, optionality, and field count.

---

## Integration proof

`tests/dto-emit.integration.test.ts` compiles inline `.tsp` programs through the
TypeSpec test-host and asserts emitted file content in the in-memory FS:

| Scenario | What is verified |
|---|---|
| `getJwks` op | `GetJwksInput.g.cs` parameterless record + `GetJwksOutput.g.cs` with `Jwk` + `get-jwks-dto.g.ts` |
| `sign` op with `@d2Redact` | `SignInput.g.cs` carries `[property: RedactData(Reason = RedactReason.PersonalInformation)] byte[] Payload` |
| Unmapped scalar (`utcDateTime`) | D2TSP001 diagnostic fires; `host.compile()` throws or `programErrors.length > 0` |

---

## REST route+policy emitter C# validation table

The TestServer host in `D2.Edge.Tests` stands up the real `JwtAuthMiddleware` pipeline (via `UseD2Auth()`) against the TypeSpec-emitted route registrations, replacing only the network-touching seams with local fakes.

| Seam | Real or fake | Notes |
|---|---|---|
| `JwtAuthMiddleware` | **Real** | Actual middleware from `D2.Shared.Auth.Http` — validates RS256 JWT, enforces scope, rejects missing bearers |
| `JwtValidator` | **Real** | Registered via `TryAddSingleton` (public path) — resolves `IJwksProvider` from DI lazily |
| `IJwksProvider` | **Fake** (`FakeJwksProvider`) | Returns an in-memory `JwksKeySetSnapshot` built from the test RSA key |
| `ISessionLivenessTracker` | **Fake** (`FakeSessionLivenessTracker`) | Always returns alive (session not revoked) |
| `ITieredCache` | **Fake** (`FakeTieredCacheStub`) | Required by `JwtAuthMiddleware`'s cache layer; no-op |
| `IKeyCustodianSignerFacade` | **Fake** (`FakeKeyCustodianSignerFacade`) | Records call count + last input; returns a configurable `D2Result` |
| `RequireAnyScope` / `RequireAllScopes` / `MarkAsD2HarmlessEndpoint` | **Real** | From `D2.Shared.Auth.Http.Endpoints` — real metadata attachment; real scope check in middleware |
| Rate-tier + CSRF markers | **Faithful seam** | `D2GeneratedRateLimitTier` / `D2GeneratedCsrfPosture` — markers are asserted PRESENT on endpoint metadata; no rate-limit or CSRF enforcement (unbuilt Edge middleware; replace-trigger: Edge rate-limit/CSRF middleware landing) |
| D2Result → ProblemDetails (MAP-ii) | **Real** | `ToProblemDetails(HttpContext)` from `D2.Shared.Auth.Http.ProblemDetails` — preserves `d2_error_code` extension |

| Test class | Scenario | Key assertion |
|---|---|---|
| `RoutePolicyEnforcementTests` | Sign route + bearer with `self.write` | 200 OK (delegates to fake façade) |
| `RoutePolicyEnforcementTests` | Sign route + bearer with wrong scope | 401 `AUTH_SCOPE_INSUFFICIENT` |
| `RoutePolicyEnforcementTests` | Sign route + bearer with no scopes | 401 `AUTH_SCOPE_INSUFFICIENT` |
| `RoutePolicyEnforcementTests` | Sign route + no bearer | 401 `AUTH_BEARER_MISSING` |
| `RoutePolicyEnforcementTests` | AllScopes route + bearer with both `self.read`+`self.write` | 200 OK |
| `RoutePolicyEnforcementTests` | AllScopes route + bearer missing one scope | 401 `AUTH_SCOPE_INSUFFICIENT` |
| `RoutePolicyEnforcementTests` | AllScopes route + no bearer | 401 `AUTH_BEARER_MISSING` |
| `RoutePolicyEnforcementTests` | Fake façade returns `ServiceUnavailable` | 503 `application/problem+json` |
| `RouteFacadeDelegationTests` | Sign route success → façade received correct input | `fake.SignCallCount == 1`, `fake.LastSignInput.Kid == kid` |
| `RouteFacadeDelegationTests` | Sign route → `ServiceUnavailable` | 503 `application/problem+json` |
| `RouteFacadeDelegationTests` | Sign route → `NotFound` | 404 `application/problem+json` |
| `RouteFacadeDelegationTests` | Sign route → `ValidationFailed` | 400 `application/problem+json` |

**D2TSP004 / D2TSP005 / D2TSP006 assertion notes**: D2TSP004 and D2TSP005 are tested in `route-policy-emitter.test.ts` via the pure-fn `onError` callback (no TypeSpec host needed) and in `route-emit.integration.test.ts` via `hasError()`. D2TSP006 (`idempotent-requires-route`) is tested directly in `route-emit.direct.test.ts` (`$onEmit_routeEmitDirect_IdempotentWithoutRoute_D2TSP006`) and in `route-emit.integration.test.ts` (`routeEmitIntegration_IdempotentWithoutRoute_D2TSP006`); catalog registration and error severity are asserted in `tests/idempotency-gate-emitter.test.ts` (`D2TSP006_IdempotentRequiresRoute_DirectCatalogTest`). Severity is `"error"` in `src/lib.ts`; the per-code severity is asserted explicitly in `tests/lib.test.ts` (all-catalog guard).

---

## Idempotency gate emitter — seam ledger

The HTTP result-replay idempotency store is an unbuilt Edge concern (PHASE_3_EDGE §1 — the `Idempotency.*` middleware lib lands when Edge ships). The emitter owns a faithful seam interface so validation is not blocked on the unbuilt consumer.

| Seam | Kind | Consumer | Replace-trigger |
|---|---|---|---|
| `D2GeneratedIdempotencyStore` (emitter-owned generated interface — `TryGetAsync<TStored>` + `StoreAsync<TStored>`) | **Faithful seam** — validates the real replay contract (store/lookup/TTL-expiry/result-replay) against an in-memory `FakeIdempotencyStore` with injectable `TimeProvider` | Unbuilt Edge `Idempotency.*` HTTP-idempotency middleware (PHASE_3_EDGE §1) | That middleware lib ships and implements `D2GeneratedIdempotencyStore` |

The `D2GeneratedIdempotencyStore` prefix signals emitter ownership and avoids a future name collision when the real Edge `IIdempotencyStore` lands (the real one can carry the un-prefixed name). The design mirrors the route emitter's `D2GeneratedRateLimitTier` / `D2GeneratedCsrfPosture` faithful-seam pattern.

---

## Idempotency gate emitter — C# validation table (`RouteIdempotencyGateTests`)

TestServer host with real `UseD2Auth()` + `FakeKeyCustodianSignerFacade` + `FakeIdempotencyStore` (injectable `FakeTimeProvider` for deterministic TTL-expiry tests).

| Seam | Real or fake | Notes |
|---|---|---|
| `JwtAuthMiddleware` / `JwtValidator` / `RequireAnyScope` | **Real** | Same pipeline as `RoutePolicyEnforcementTests` — JWT, scope enforcement, MAP-ii |
| `IKeyCustodianSignerFacade` | **Fake** (`FakeKeyCustodianSignerFacade`) | Records `SignCallCount` + `SignDerivedCallCount`; returns canned `D2Result` |
| `D2GeneratedIdempotencyStore` | **Faithful in-memory seam** (`FakeIdempotencyStore`) | Real TTL-expiry via injectable `TimeProvider` (`FakeTimeProvider`); real store/lookup; `ServiceUnavailable` when faulted |

| Test | Scenario | Key assertion |
|---|---|---|
| `SignRoute_DuplicateIdempotencyKey_ReturnsStoredResult_WithoutCallingFacade` | Dup key (header) | 2nd request replays stored result; `SignCallCount == 1`; `TryGetCallCount == 2` |
| `SignRoute_MissingIdempotencyKeyHeader_Returns400ValidationFailed` | Missing header | 400 `application/problem+json`; `SignCallCount == 0` |
| `SignRoute_WhitespaceIdempotencyKey_Returns400ValidationFailed` | Whitespace-only header | 400; `SignCallCount == 0` (Falsey guard) |
| `SignRoute_StoreReadOutage_FailsOpen_DelegateStillInvoked` | Store read outage | Gate fails-open; 200 OK; `SignCallCount == 1` |
| `SignRoute_StoredFailure_ReplaysFailureOnSecondCall` | Stored failure | First call stores 503; second call replays 503 without re-invoking façade |
| `SignDerivedRoute_DuplicateKid_ReturnsStoredResult_WithoutCallingFacade` | Derived dup (same kid) | 2nd request replays; key = `SHA-256(kid)`; `SignDerivedCallCount == 1` |
| `SignDerivedRoute_DifferentKid_NoCacheHit_FacadeCalledAgain` | Derived different kids | 2 distinct keys; `SignDerivedCallCount == 2` |
| `SignRoute_ExpiredIdempotencyKey_ReExecutes_AfterTtlElapses` | TTL-expiry (clock-driven) | Before expiry: replay, `SignCallCount == 1`. After `FakeTimeProvider.Advance(86401s)`: expired → miss → `SignCallCount == 2` (deterministic, no `Task.Delay`) |

---

## REST route+policy emitter — byte-parity table

| Committed fixture | Emitter call | Key assertion | Test file | Test name |
|---|---|---|---|---|
| `TypeSpecRoute/Generated/SignRouteRegistration.g.cs` (also contains marker records) | `emitRoutePolicy({ opName: "sign", verb: "post", ... })` | Byte-identical to `SIGN_ROUTE_REGISTRATION_FIXTURE` | `tests/route-policy-emitter.test.ts` | `byteParity_SignRouteRegistration_CommittedFixtureIdentical > regenerated SignRouteRegistration.g.cs is byte-identical to the committed fixture` |
| `TypeSpecRoute/Generated/AllScopesRouteRegistration.g.cs` | `emitRoutePolicy({ opName: "allScopes", verb: "get", ... })` | Byte-identical to `ALL_SCOPES_ROUTE_REGISTRATION_FIXTURE`; verb is GET (emits `[AsParameters]` binding) | `tests/route-policy-emitter.test.ts` | `byteParity_AllScopesRouteRegistration_CommittedFixtureIdentical > regenerated AllScopesRouteRegistration.g.cs is byte-identical to committed fixture` |
| `TypeSpecRoute/Generated/SignRouteRegistration.g.cs` (header-gated form — `@d2Idempotent("header", 86400)`) | `emitRoutePolicy({ ..., idempotency: { keySource: "header", ttlSeconds: 86400, fields: [] } })` | Byte-identical to `SIGN_ROUTE_REGISTRATION_GATED_FIXTURE` (gate woven in: `D2GeneratedIdempotencyStore store` param, header read, `Falsey()` guard, `TryGetAsync` replay, `StoreAsync` with `TimeSpan.FromSeconds(86400)`) | `tests/idempotency-gate-emitter.test.ts` | `byteParity_SignRouteRegistration_Gated_CommittedFixtureIdentical > regenerated SignRouteRegistration.g.cs (gated) is byte-identical to the committed fixture` |
| `TypeSpecRoute/Generated/SignDerivedRouteRegistration.g.cs` (derived-gated form — `@d2Idempotent("derived", 3600, "kid")`) | `emitRoutePolicy({ ..., idempotency: { keySource: "derived", ttlSeconds: 3600, fields: ["Kid"] } })` | Byte-identical to `SIGN_DERIVED_ROUTE_REGISTRATION_FIXTURE` (SHA-256 derived key over `input.Kid`, `TimeSpan.FromSeconds(3600)`, no Falsey guard) | `tests/idempotency-gate-emitter.test.ts` | `byteParity_SignDerivedRouteRegistration_CommittedFixtureIdentical > regenerated SignDerivedRouteRegistration.g.cs is byte-identical to the committed fixture` |
| `TypeSpecRoute/Generated/D2GeneratedIdempotencyStore.g.cs` (emitter-owned seam) | `emitIdempotencyStoreSeam("D2.Edge.Tests.TypeSpecRoute.Generated", "contracts/typespec/fixtures/sign-shaped.tsp")` | Byte-identical to `IDEMPOTENCY_STORE_SEAM_FIXTURE` (public interface with `TryGetAsync<TStored>` + `StoreAsync<TStored>`, XML docs, auto-generated banner) | `tests/idempotency-gate-emitter.test.ts` | `byteParity_IIdempotencyStore_SeamFixtureIdentical > regenerated D2GeneratedIdempotencyStore.g.cs is byte-identical to the committed fixture` |

**Deliberate-drift non-vacuity guards**: each byte-parity describe block contains a second test that mutates the fixture by one token and asserts the regenerated content does NOT match. `SignRouteRegistration`: `.replace("MapPost", "MapGet")`; `AllScopesRouteRegistration`: `.replace("RequireAllScopes", "RequireAnyScope")`; `SignRouteRegistration` (gated): `.replace("TryGetAsync", "TryGetAsyncDRIFTED")`; `SignDerivedRouteRegistration`: `.replace("SHA256.HashData", "SHA256.HashDataDRIFTED")`; `D2GeneratedIdempotencyStore`: `.replace("TryGetAsync", "TryGetAsyncDRIFTED")`.

---

## gRPC harness C# validation table

Committed C# fixture files exercised by the in-memory gRPC harness in `D2.Edge.Tests`:

| Fixture | Validated by | Key assertion |
|---|---|---|
| `key_custodian_signer_sign.g.proto` | `ProtoRoundTripTests` (5 tests) | `SignRequest` / `SignResponse` proto3 messages compile and round-trip via `Grpc.Tools`-generated types |
| `KeyCustodianSignerService.g.cs` | `GrpcServiceImplTests.Sign_Success_ReturnsSignatureFromFacade` | Proto→DTO mapping, façade delegation (`IKeyCustodianSignerFacade.SignAsync`), and DTO→proto mapping work end-to-end via in-process `TestServer` (re-pointed from `ISignHandler` per the `@d2InProcess` delegation rule) |
| `KeyCustodianSignerService.g.cs` | `GrpcServiceImplTests.Sign_FacadeFailure_ThrowsRpcExceptionInternal` | `D2Result` failure from the façade maps to `RpcException(StatusCode.Internal)` with empty detail (no info leak) |
| `KeyCustodianSignerService.g.cs` | `GrpcServiceImplTests.Sign_DelegatesThroughFacade_RecordsCallCount` | Service calls the façade (not the handler) — call count asserted, proving the delegation target is the fixture façade |
| `SignTransportMappers.g.cs` | Exercised by `GrpcServiceImplTests` via `KeyCustodianSignerService.g.cs` | `ToSignInput()` / `ToProtoSignOutput()` C# 14 extension members compile and map correctly; mapper is unchanged by the delegation target |

---

## Coverage summary

| Metric | Result |
|---|---|
| Lines | 100% |
| Branches | 100% |
| Functions | 100% |
| Statements | 100% |
| Test files | 25 |
| Total tests | 472 |

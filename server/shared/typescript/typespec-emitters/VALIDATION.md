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

**D2TSP004 / D2TSP005 assertion notes**: both diagnostics are tested in `route-policy-emitter.test.ts` via the pure-fn `onError` callback (no TypeSpec host needed) and in `route-emit.integration.test.ts` via `hasError()`. Severity is `"error"` in `src/lib.ts`; the per-code severity is asserted explicitly in `tests/lib.test.ts`.

---

## REST route+policy emitter — byte-parity table

| Committed fixture | Emitter call | Key assertion | Test file | Test name |
|---|---|---|---|---|
| `TypeSpecRoute/Generated/SignRouteRegistration.g.cs` (also contains marker records) | `emitRoutePolicy({ opName: "sign", verb: "post", ... })` | Byte-identical to `SIGN_ROUTE_REGISTRATION_FIXTURE` | `tests/route-policy-emitter.test.ts` | `byteParity_SignRouteRegistration_CommittedFixtureIdentical > regenerated SignRouteRegistration.g.cs is byte-identical to the committed fixture` |
| `TypeSpecRoute/Generated/AllScopesRouteRegistration.g.cs` | `emitRoutePolicy({ opName: "allScopes", verb: "get", ... })` | Byte-identical to `ALL_SCOPES_ROUTE_REGISTRATION_FIXTURE`; verb is GET (emits `[AsParameters]` binding) | `tests/route-policy-emitter.test.ts` | `byteParity_AllScopesRouteRegistration_CommittedFixtureIdentical > regenerated AllScopesRouteRegistration.g.cs is byte-identical to committed fixture` |

**Deliberate-drift non-vacuity guards**: each byte-parity describe block contains a second test that mutates the fixture by one token and asserts the regenerated content does NOT match. `SignRouteRegistration`: `.replace("MapPost", "MapGet")`; `AllScopesRouteRegistration`: `.replace("RequireAllScopes", "RequireAnyScope")`.

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
| Test files | 24 |
| Total tests | 402 |

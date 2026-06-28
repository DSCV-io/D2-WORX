<!--
Copyright (c) DCSV. All rights reserved.
-->

# @d2/typespec-emitters — Emitter Validation Ledger

Byte-parity and structural validation rows for the C# + TS DTO emitters.
Each row maps a committed fixture to its regeneration guarantee.

---

## C# fixture parity table

| Fixture file                                                                                    | Emitter call                                                                                                                                                                                                      | Key assertion                                                                            | Test file                              | Test name                                                                                                                       |
| ----------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `clients/GetJwksInput.g.cs` (ns `D2.Edge.KeyCustodian.Clients`)                                 | `emitCsharpDtos("getJwks", "D2.Edge.KeyCustodian.Clients", "contracts/typespec/key-custodian/key-custodian.tsp", [], [], [])`                                                                                     | Byte-identical to `GET_JWKS_INPUT_FIXTURE`                                               | `tests/byte-parity.test.ts`            | `byteParity_GetJwksInput_CommittedFixtureIdentical`                                                                             |
| `clients/GetJwksOutput.g.cs` (ns `D2.Edge.KeyCustodian.Clients`)                                | `emitCsharpDtos("getJwks", "D2.Edge.KeyCustodian.Clients", ..., [], outputFields, [nested("Jwk", ...)])`                                                                                                          | Byte-identical to `GET_JWKS_OUTPUT_FIXTURE`                                              | `tests/byte-parity.test.ts`            | `byteParity_GetJwksOutput_CommittedFixtureIdentical`                                                                            |
| `SignFixtureInput.g.cs`                                                                                | `emitCsharpDtos("sign", ..., inputFields, [], [])`                                                                                                                                                                | Byte-identical to `SIGN_INPUT_FIXTURE`                                                   | `tests/byte-parity.test.ts`            | `byteParity_SignFixtureInput_CommittedFixtureIdentical`                                                                                |
| `TemporalFixtureInput.g.cs`                                                                            | `emitCsharpDtos("temporal", "D2.Edge.Tests.TypeSpecDto.Generated", "contracts/typespec/fixtures/temporal-shaped.tsp", inputFields, outputFields, nested)`                                                         | Byte-identical to `TEMPORAL_INPUT_FIXTURE` (every temporal scalar + 2 composite refs)    | `tests/byte-parity.test.ts`            | `byteParity_TemporalFixtureInput_CommittedFixtureIdentical`                                                                            |
| `TemporalFixtureOutput.g.cs`                                                                           | `emitCsharpDtos("temporal", ..., inputFields, outputFields, [nested(ZonedInstantWire), nested(LocalAnchoredEventWire)])`                                                                                          | Byte-identical to `TEMPORAL_OUTPUT_FIXTURE` (mirror + 2 nested composite records)        | `tests/byte-parity.test.ts`            | `byteParity_TemporalFixtureOutput_CommittedFixtureIdentical`                                                                           |
| `sign_fixture_signer_sign_fixture.g.proto`                                                             | `emitProto("sign", "SignFixtureSigner", "Sign", "unary", "d2.signfixtures.v1", "D2.Services.Protos.SignFixtures.V1", ...)`                                                                             | Byte-identical to `SIGN_PROTO_FIXTURE`                                                   | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_SignProto_CommittedFixtureIdentical`                                                                                |
| `SignFixtureSignerService.g.cs`                                                                | `emitGrpcService("sign", "SignFixtureSigner", "Sign", ..., { kind: "facade", typeName: "ISignFixtureSignerFacade", methodName: "SignAsync", targetNamespace: "D2.Edge.Tests.TypeSpecRoute.Generated.Facade" })` | Byte-identical to `SIGN_SERVICE_FIXTURE` (façade delegation — op carries `@d2InProcess`) | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_SignFixtureSignerService_FacadeDelegation_CommittedFixtureIdentical`                                               |
| `SignFixtureTransportMappers.g.cs`                                                                     | `emitGrpcService(...)` (mappers file)                                                                                                                                                                             | Byte-identical to `SIGN_MAPPER_FIXTURE`                                                  | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_SignFixtureTransportMappers_CommittedFixtureIdentical`                                                                     |
| `clients/IKeyCustodianApi.g.cs` (ns `D2.Edge.KeyCustodian.Clients`)                             | `emitFacade("KeyCustodian", [{ opName: "getJwks", ... }], "D2.Edge.KeyCustodian.Clients", "D2.Edge.KeyCustodian.App.Application")` (interface file)                                                               | Byte-identical to `INTERFACE_FIXTURE`                                                    | `tests/facade-emitter.test.ts`         | `facadeEmitter_ByteGate_Interface > regenerated IKeyCustodianApi.g.cs is byte-identical to the committed fixture`               |
| `app/Application/KeyCustodianApi.g.cs` (ns `D2.Edge.KeyCustodian.App.Application`)              | `emitFacade(...)` (impl file, index 1)                                                                                                                                                                            | Byte-identical to `IMPL_FIXTURE`                                                         | `tests/facade-emitter.test.ts`         | `facadeEmitter_ByteGate_Impl > regenerated KeyCustodianApi.g.cs is byte-identical to the committed fixture`                     |
| `app/Application/KeyCustodianClientsGenerated.g.cs` (ns `D2.Edge.KeyCustodian.App.Application`) | `emitFacade(...)` (DI extension file, index 2)                                                                                                                                                                    | Byte-identical to `DI_FIXTURE`                                                           | `tests/facade-emitter.test.ts`         | `facadeEmitter_ByteGate_DiExtension > regenerated KeyCustodianClientsGenerated.g.cs is byte-identical to the committed fixture` |

**Deliberate-drift non-vacuity guard**: each byte-parity describe block contains a second
test that mutates the respective fixture by one byte and asserts the regenerated content
does NOT match (`not.toBe(driftedFixture)`). This proves the byte gate is not a tautology
comparing a buffer to itself — the guard covers `GetJwksInput`, `GetJwksOutput`, `SignFixtureInput`,
`TemporalFixtureInput` (mutates `DateTimeOffset PastInstant`), and `TemporalFixtureOutput` (mutates the
`ZonedInstantWire` composite record).

---

## Author-pin guarantee, D2TSP009, and `@d2Reserved` validation ledger

| What is validated | Against what | Test |
| ------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------- | ---- |
| Author-pinned field numbers (`@d2Field(N)`) are used verbatim in emitted proto — NOT assigned positionally | `byteParity_SignProto_CommittedFixtureIdentical` (the committed `sign_fixture_signer_sign_fixture.g.proto` carries explicit field numbers, byte-identical to regen) + deliberate-drift negative | `tests/proto-grpc-byte-parity.test.ts` |
| An unpinned proto-bound field triggers D2TSP009 (loud failure, returns `undefined`) | `emitProto` pure-fn with a `FieldInfo` carrying `fieldNumber: undefined` → `onError` called with code `"unpinned-proto-field"`, return value `undefined` | `tests/proto-emitter.test.ts` (`emitProto_UnpinnedField_LoudFailure`) |
| D2TSP009 is registered in `src/lib.ts` with severity `"error"` | `src/lib.ts` catalog + `tests/lib.test.ts` all-catalog-severity guard | `tests/lib.test.ts` |
| `reserved` lines are emitted for `@d2Reserved` entries (ascending, deduped, range-collapsed numbers + quoted names) | `emitProto` pure-fn with a `requestReserved` / `responseReserved` payload → committed `sign.proto` fixture carries the `reserved` block; direct `emitMessage` unit tests cover range-collapse; reserved-names dedup is pinned end-to-end in `protoGrpcEmitIntegration_D2Reserved_DuplicateNamesDeduplicated` | `tests/proto-emitter.test.ts` (range-collapse unit tests); `tests/proto-grpc-emit.integration.test.ts` (`protoGrpcEmitIntegration_D2Reserved_DuplicateNamesDeduplicated` for names-dedup) |
| An empty `@d2Reserved` payload emits no `reserved` lines | `emitProto` with `requestReserved: undefined` → no `reserved` line in the emitted proto | `tests/proto-emitter.test.ts` |

**Replace-trigger**: if the `@d2Field` / `@d2Reserved` decorator signatures change (see `@d2/typespec-decorators` `src/decorators.ts`), the emitter's `resolveProtoFields` caller and `FieldInfo.fieldNumber` shape must be updated in lockstep; the byte-parity gate will catch divergence.

---

## C# structural validation table

| Live type                                                                | Generated type                                                                                       | Validated by                                                                                                   |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `D2.Edge.KeyCustodian.Clients.GetJwksInput` (parameterless)              | `D2.Edge.KeyCustodian.Clients.GetJwksInput` (committed `clients/GetJwksInput.g.cs`)                  | `GetJwksTransportDtoTests.GetJwksInput_IsParameterless`                                                        |
| `D2.Edge.KeyCustodian.Clients.GetJwksOutput` (`IReadOnlyList<Jwk> Keys`) | `D2.Edge.KeyCustodian.Clients.GetJwksOutput` (committed `clients/GetJwksOutput.g.cs`)                | `GetJwksTransportDtoTests.GetJwksOutput_HasKeysProperty_OfCorrectType`                                         |
| `D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk` (6 public members)        | `D2.Edge.KeyCustodian.Clients.Jwk` (6-field positional record)                                       | `GetJwksTransportDtoTests.Jwk_MatchesDomainVoPublicShape`                                                      |
| `[property: RedactData]` on `SignFixtureInput.Payload`                          | `[property: RedactData(Reason = RedactReason.PersonalInformation)]` on generated `SignFixtureInput.Payload` | `TypeSpecDtoValidationTests.GeneratedSignFixtureInput_PayloadProperty_IsRedactedByRealPolicy` (real Serilog pipeline) |

**Redaction proof**: `GeneratedSignFixtureInput_PayloadProperty_IsRedactedByRealPolicy` builds a
real `LoggerConfiguration().Destructure.With<RedactDataDestructuringPolicy>()` pipeline (not a
mock), logs the generated `SignFixtureInput` record, and asserts the rendered output contains
`"[REDACTED: PersonalInformation]"` and does NOT contain `"SECRET_PAYLOAD"`.

**Transport-vs-domain-VO divergence note**: the domain `Jwk` VO has 3 positional ctor params + 3
init-only properties with constant defaults (`Kty="RSA"`, `Use="sig"`, `Alg="RS256"`). The
transport `D2.Edge.KeyCustodian.Clients.Jwk` is a 6-field positional record. Equivalence is
validated by public-member-set comparison (6 public members, same names and types), NOT by
constructor arity. The transport DTO lives in `D2.Edge.KeyCustodian.Clients` — a separate project
from the domain VO — because it is consumed by callers who must not depend on the domain layer.

---

## TS fixture parity table

| Shape assertion                                                                      | Test file                   | Test name                      |
| ------------------------------------------------------------------------------------ | --------------------------- | ------------------------------ |
| `get-jwks-dto.g.ts` contains `export interface GetJwksInput`, `GetJwksOutput`, `Jwk` | `tests/byte-parity.test.ts` | `byteParity_GetJwksDto_TsFile` |
| `readonly kid: string;` inside `Jwk` interface                                       | `tests/byte-parity.test.ts` | `byteParity_GetJwksDto_TsFile` |
| `readonly keys: readonly Jwk[];` inside `GetJwksOutput`                              | `tests/byte-parity.test.ts` | `byteParity_GetJwksDto_TsFile` |

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

| Scenario                                | What is verified                                                                                                                                                                                                                                                                                                                                                                                           |
| --------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `getJwks` op                            | `GetJwksInput.g.cs` parameterless record + `GetJwksOutput.g.cs` with `Jwk` + `get-jwks-dto.g.ts`                                                                                                                                                                                                                                                                                                           |
| `sign` op with `@d2Redact`              | `SignFixtureInput.g.cs` carries `[property: RedactData(Reason = RedactReason.PersonalInformation)] byte[] Payload`                                                                                                                                                                                                                                                                                                |
| `temporal` op (scalars + composites)    | `TemporalFixtureInput.g.cs`/`TemporalFixtureOutput.g.cs` emit every temporal scalar (instant→`DateTimeOffset`, plain/duration→`string`, optional→`T?`) + the two composite records as nested siblings; `temporal-fixture-dto.g.ts` mirrors (all `string`, `?:` optional, no `\| null`)                                                                                                                                           |
| `enums` op (every enum/union shape)     | `EnumFixtureOutput.g.cs` emits sibling `public enum` blocks with `[JsonConverter(typeof(JsonStringEnumConverter))]` + `[JsonStringEnumMemberName("third-party")]` (NOT `[EnumMember]`/`System.Runtime.Serialization`) + the `Low = 0` int backing + the `EnumOutputInlineState` synthetic enum; `enum-fixture-dto.g.ts` emits `const`-objects + derived types (no TS `enum` keyword, no Zod, S-2 value === member name) |
| mixed-primitive union (in-process)      | D2TSP007 fires; no partial DTO file emitted                                                                                                                                                                                                                                                                                                                                                                |
| mixed-primitive union (`@d2GrpcMethod`) | D2TSP007 fires on the proto path; no partial `.proto` file emitted                                                                                                                                                                                                                                                                                                                                         |
| Unmapped scalar (`unixTimestamp32`)     | D2TSP001 diagnostic fires; `host.compile()` throws or `programErrors.length > 0` (the mapped temporal scalars no longer trip it — a genuinely-unmapped built-in does)                                                                                                                                                                                                                                      |

---

## Temporal scalar + composite validation (real `D2.Shared.Time` / `@d2/time` seams)

The temporal DTO emission is validated against the REAL temporal domain libraries — NOT test
doubles — via an adversarial round-trip matrix driven by the shared cross-language fixture
`contracts/temporal/temporal-adversarial.fixture.json` (extended with a `scalarRoundTripFixtures`
section). The SAME wire value materializes to the equivalent domain value in BOTH languages.

| Concern                                     | C# (`D2.Edge.Tests` `TemporalRoundTripTests`)                                                                                                                                                                          | TS (`@d2/time` `temporal-round-trip.test.ts`)                                                                        |
| ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| RT-1..6 per-type round-trip                 | `Instant` / `OffsetDateTime` / `LocalDate` / `LocalTime` / `LocalDateTime` / `Duration` ↔ wire ↔ domain (REAL NodaTime patterns)                                                                                       | `Temporal.Instant`/`PlainDate`/`PlainTime`/`PlainDateTime`/`Duration` ↔ wire ↔ domain                                |
| RT-7 ZonedInstant — **IANA NAME survives**  | `ZonedInstant.Create` → `ZonedInstantWire` → `Create`; asserts the canonical IANA NAME (not just the offset) survives                                                                                                  | `ZonedInstant.create` → wire → `create`; same IANA-name-survival assertion                                           |
| RT-8 LocalAnchoredEvent composite           | `LocalAnchoredEvent.Create` + `ComputeNextFire` round-trip through `LocalAnchoredEventWire` (all 3 fields agree)                                                                                                       | `LocalAnchoredEvent.create` + `computeNextFire` round-trip                                                           |
| AD-1..3 DST gap/overlap (US/EU/AU)          | `ComputeNextFire` matches the fixture `expectedUtc` (NodaTime `LenientResolver`)                                                                                                                                       | `computeNextFire` matches (Temporal `disambiguation:"compatible"`)                                                   |
| AD-4 invalid IANA → `ValidationFailed`      | `Create` returns `ValidationFailed(INVALID_IANA_IDENTIFIER)` — never throws                                                                                                                                            | `create` returns `validationFailed(TK…INVALID_IANA_IDENTIFIER)`                                                      |
| AD-5 fixed-offset rejected as IANA          | `+05:00` / `-08:00` / `UTC+5` → `ValidationFailed` (the core reason the composite exists)                                                                                                                              | same rejection                                                                                                       |
| AD-6 IANA alias normalization survives      | `US/Pacific`→`America/Los_Angeles`, `Asia/Saigon`→`Asia/Ho_Chi_Minh` survive the wire                                                                                                                                  | same canonicalization survives                                                                                       |
| AD-7 leap day / impossible date             | Feb 29 leap valid; Feb 29 non-leap → `ArgumentOutOfRangeException` at the ctor (documented C#/TS divergence)                                                                                                           | Feb 29 non-leap → `RangeError` (documented divergence — each asserts its own)                                        |
| AD-8 year boundary / min-max instant        | boundary + `DateTimeOffset.Min/Max` survive the wire                                                                                                                                                                   | boundary instant survives                                                                                            |
| AD-9 sub-second precision (Duration)        | `Duration` nanosecond value lossless; sub-second ISO decimal-fraction seconds round-trip LOSSLESSLY via `D2.Shared.Time.IsoDuration` (int64-ns, no float) — `"PT0.123456789S"` → `Duration` → `"PT0.123456789S"` exact | `Temporal.Duration` round-trips the SAME shared-fixture ISO decimal seconds losslessly — cross-language value parity |
| AD-10 no-invented-offset (plain-local)      | `plainDate`/`plainTime`/`plainDateTime` wire strings carry no `+`/`Z`                                                                                                                                                  | same — offset-free assertion                                                                                         |
| AD-11 optional `nextFireUtc` null→undefined | `DateTimeOffset?` null round-trips to `null`                                                                                                                                                                           | absent → `undefined` (prefer-undefined boundary)                                                                     |
| AD-12 historical tzdb offset                | 1950 New York date uses the tzdb-correct historical offset (tzdb-version-sensitive)                                                                                                                                    | same via Temporal tzdb                                                                                               |
| NV-1 byte-gate deliberate-drift             | (TS) `byte-parity.test.ts` mutates `TemporalFixtureInput`/`TemporalFixtureOutput` fixtures by one byte → regenerated output must NOT match                                                                                           | —                                                                                                                    |
| NV-2 comparator non-tautology               | 1-second / DST-policy / IANA-canonicalization divergences are DETECTED                                                                                                                                                 | same three divergence guards                                                                                         |
| NV-3 loud-fail intact                       | (TS) `scalar-registry.test.ts` — a genuinely-unknown scalar STILL throws D2TSP001 after temporal was added                                                                                                             | —                                                                                                                    |

**Sub-second `duration` (lossless, both languages)**: ISO-8601 permits a decimal fraction on the
seconds field (`PT0.123456789S`). `Temporal.Duration` round-trips that notation natively to
nanoseconds; NodaTime ships NO built-in pattern that parses decimal-fraction ISO seconds
(`DurationPattern.Roundtrip` is the colon form; `PeriodPattern` uses explicit unit fields). The .NET
side therefore uses the `D2.Shared.Time.IsoDuration` helper (`Parse`/`Format`) — it computes total
nanoseconds as an `Int128`/int64 integer (a 9-digit right-pad of the fraction; NO `double`/`float`),
so the wire stays an ISO-8601 STRING and sub-second durations round-trip LOSSLESSLY:
`"PT0.123456789S"` → `Duration` → `"PT0.123456789S"` exact in C#, the same value in TS. Whole-unit
durations (`PT1H30M`, `P1DT2H3M4S`, `PT45S`) and unbalanced Temporal-emitted forms (`PT90M`,
`PT3600S`) round-trip by VALUE in both. The shared fixture
(`temporal-adversarial.fixture.json`, with sub-second entries added) drives both halves;
`AD9_Duration_SubSecondIsoDecimalNotation_RoundTripsLossless_BothLanguages` (C#) +
`AD9_duration_subSecondNanos_roundTripsLossless_bothLanguages` (TS) assert cross-language parity.
Malformed / out-of-range ISO → `ValidationFailed(common_time_INVALID_DURATION)` (error-as-value,
never a throw). The helper's own adversarial suite is `IsoDurationTests` (D2.Shared.Tests).

---

## Enum / string-literal-union validation (real `JsonStringEnumConverter` / `SerializerOptions.SR_Web` + the proto-string bridge)

Enum/union DTO emission is validated against the REAL `JsonStringEnumConverter`
(via `SerializerOptions.SR_Web` — `D2.Shared.Utilities.Serialization`) + the REAL
Grpc.Tools-generated proto types + the REAL generated transport mappers — no test
doubles — driven by the shared cross-language fixture
`contracts/enum/enum-parity.fixture.json`. The single load-bearing claim: the
SAME wire string materializes to the SAME enum member across C# JSON, the proto
`string`-field path, and TS; an unknown wire value fails LOUD on all three (NO
fallback sentinel).

| Concern                                                            | C# (`D2.Edge.Tests` `EnumWireRoundTripTests`)                                                                                                                                                                                        | TS (`enum-wire-round-trip.test.ts`)                                            |
| ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------ |
| P-1 bare-member enum (S-1)                                         | `KeyKind.Rsa` ⇄ `"Rsa"` via `SR_Web`                                                                                                                                                                                                 | const-object value === `"Rsa"`; reverse-lookup hits                            |
| P-2 explicit-int enum (S-2) — **wire is the NAME**                 | `Level.High` → `"High"` (NOT `10`); asserts no digit in the wire                                                                                                                                                                     | `Level.High === "High"`; const values never contain `0`/`10`                   |
| P-3 string-literal union (S-3)                                     | `Status.Active` ⇄ `"active"` (lowercase literal via `[JsonStringEnumMemberName]`)                                                                                                                                                    | `Status.Active === "active"`                                                   |
| P-4 non-identifier literal (S-3 + member-name attr)                | `AccountKind.ThirdParty` ⇄ `"third-party"` via `[JsonStringEnumMemberName("third-party")]`                                                                                                                                           | `AccountKind.ThirdParty === "third-party"`                                     |
| **AD-1 unknown wire value → fail-loud (all 3)**                    | JSON: `"Quantum"` → `JsonException`. proto mapper: `ParseKeyKindWire("Quantum")` → `ValidationFailed` (400). NO `Unknown` sentinel                                                                                                   | const-object membership MISSES (`memberForWire` → undefined); no fallback      |
| AD-2 case-insensitivity (documented divergence)                    | `JsonStringEnumConverter` is case-INSENSITIVE on read (`"rsa"`/`"RSA"` → `Rsa`) — pinned                                                                                                                                             | TS reverse-lookup is case-SENSITIVE → `"rsa"`/`"RSA"` MISS (divergence pinned) |
| AD-3 null for required enum                                        | JSON `null` into non-nullable `KeyKind` → `JsonException`                                                                                                                                                                            | —                                                                              |
| AD-4 empty/whitespace wire value                                   | `""`/`" "` → `JsonException`                                                                                                                                                                                                         | covered by the unknown-value membership-miss sweep                             |
| gRPC end-to-end success — proto string ⇄ enum (request + response) | `SignWithKindAsync(key_kind: "Aes")` → handler receives `KeyKind.Aes`; the response `KeyKind.Secret` serializes back to `reply.Data.KeyKind == "Secret"` (server `ToWire`)                                                           | —                                                                              |
| **gRPC inbound fail-loud — handler NEVER invoked**                 | `SignWithKindAsync(key_kind: "Quantum")` → 400 rides the envelope; `handler.CallCount == 0` (the mapper rejected the request)                                                                                                        | —                                                                              |
| **gRPC RESPONSE-enum parse — client inbound (symmetric)**          | client `ToSignWithKindFixtureOutput()` returns `D2Result<<Output>>`: valid `key_kind` → parsed enum; `"Quantum"/"rsa"/""/" "` → `ValidationFailed` (400), null data (NO fallback) — the inbound CLIENT analogue of the server request parse | —                                                                              |
| Cross-language parity                                              | every shared-fixture member's wire round-trips through `ParseKeyKindWire` here                                                                                                                                                       | const-object values === the SAME fixture wire strings                          |

**The `[JsonStringEnumMemberName]` finding**: System.Text.Json's
`JsonStringEnumConverter` honors `[JsonStringEnumMemberName("…")]` (the .NET 9+
attribute) for a custom wire name — it does NOT honor `[EnumMember]` (a
`DataContract`/Newtonsoft attribute the converter silently ignores). The C# DTO
emitter therefore emits `[JsonStringEnumMemberName]`; P-3/P-4 above pin that the
lowercase/hyphenated literal is the actual JSON wire form (these tests caught an
initial `[EnumMember]` emission that produced the WRONG wire string — the member
name instead of the literal).

| Byte-gate                                                                                                                                                                                                                                                                  | Emitter call                                                                | Test                                                                                                                                                                                                                                       |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `EnumsInput/Output.g.cs` + `enum-fixture-dto.g.ts`                                                                                                                                                                                                                                | `emitCsharpDtos`/`emitTsDtos` over the `enums`-op walk (with `nestedEnums`) | `byte-parity.test.ts > byteParity_EnumsDto_CommittedFixtures` (+ deliberate-drift)                                                                                                                                                         |
| `enum_fixtures_signer_sign_with_kind_fixture.g.proto` (`string` enum)                                                                                                                                                                                                              | `emitProto` over the `signWithKind` fields                                  | `byte-parity.test.ts > byteParity_SignWithKindEnumGrpc_CommittedFixtures`                                                                                                                                                                  |
| `SignWithKindFixtureTransportMappers.g.cs` (the enum bridge)                                                                                                                                                                                                                      | `emitGrpcService` over the `signWithKind` fields                            | `byte-parity.test.ts > byteParity_SignWithKindEnumGrpc_CommittedFixtures` (+ drift)                                                                                                                                                        |
| `SignWithKindClientMappers.g.cs` + `EnumFixturesGrpcClient.g.cs` (the response-enum client parse + `BubbleFail` surfacing)                                                                                                                                                 | `emitGrpcClient` over the `signWithKind` op (response carries an enum)      | `byte-parity.test.ts > byteParity_SignWithKindEnumGrpcClient_CommittedFixtures` (+ drift)                                                                                                                                                  |
| `ISignFixtureGrpcClient.g.cs` + `SignFixtureGrpcClient.g.cs` + `SignFixtureClientMappers.g.cs` + `SignFixtureGrpcClientsGenerated.g.cs` + `SignFixtureClientKeys.g.cs` (the MAIN `sign` gRPC client — interface / captured-envelope impl / wire↔DTO mappers / DI ext / pipeline keys) | `emitGrpcClient` + `emitClientKeys` over the `sign` op                      | `proto-grpc-byte-parity.test.ts > byteParity_{ISignFixtureGrpcClient,SignFixtureGrpcClient,SignFixtureClientMappers,SignFixtureGrpcClientsGenerated,SignFixtureClientKeys}_CommittedFixtureIdentical` (5 blocks, each `+ *DRIFTED` non-vacuity guard) |
| `SignWithKindFixtureInput/Output.g.cs` (+ the co-located `KeyKind` enum) + `sign-with-kind-fixture-dto.g.ts` + `ISignWithKindFixtureHandler.g.cs` + `EnumFixturesSignerService.g.cs` + `IEnumFixturesGrpcClient.g.cs` + `EnumFixturesGrpcClientsGenerated.g.cs` + `SignWithKindClientKeys.g.cs` (the remaining enum-module DTO / handler / service / client-interface / DI / keys files — completeness sweep) | `emitCsharpDtos`/`emitTsDtos` (DTOs, with `nestedEnums`), `emitHandlerInterface`, `emitGrpcService` (service file, index 0), `emitGrpcClient` (interface index 0 + DI index 3), `emitClientKeys` over the `signWithKind` op | `byte-parity.test.ts > byteParity_SignWithKindEnum{Dtos,HandlerAndService,ClientModule}_CommittedFixtures` (3 blocks, each `+ *DRIFTED` non-vacuity guard) |

---

## REST route+policy emitter C# validation table

The TestServer host in `D2.Edge.Tests` stands up the real `JwtAuthMiddleware` pipeline (via `UseD2Auth()`) against the TypeSpec-emitted route registrations, replacing only the network-touching seams with local fakes.

| Seam                                                                | Real or fake                              | Notes                                                                                                                                                                                                                                                                                                                                                            |
| ------------------------------------------------------------------- | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `JwtAuthMiddleware`                                                 | **Real**                                  | Actual middleware from `D2.Shared.Auth.Http` — validates RS256 JWT, enforces scope, rejects missing bearers                                                                                                                                                                                                                                                      |
| `JwtValidator`                                                      | **Real**                                  | Registered via `TryAddSingleton` (public path) — resolves `IJwksProvider` from DI lazily                                                                                                                                                                                                                                                                         |
| `IJwksProvider`                                                     | **Fake** (`FakeJwksProvider`)             | Returns an in-memory `JwksKeySetSnapshot` built from the test RSA key                                                                                                                                                                                                                                                                                            |
| `ISessionLivenessTracker`                                           | **Fake** (`FakeSessionLivenessTracker`)   | Always returns alive (session not revoked)                                                                                                                                                                                                                                                                                                                       |
| `ITieredCache`                                                      | **Fake** (`FakeTieredCacheStub`)          | Required by `JwtAuthMiddleware`'s cache layer; no-op                                                                                                                                                                                                                                                                                                             |
| `ISignFixtureSignerFacade`                                         | **Fake** (`FakeSignFixtureSignerFacade`) | Records call count + last input; returns a configurable `D2Result`                                                                                                                                                                                                                                                                                               |
| `RequireAnyScope` / `RequireAllScopes` / `MarkAsD2HarmlessEndpoint` | **Real**                                  | From `D2.Shared.Auth.Http.Endpoints` — real metadata attachment; real scope check in middleware                                                                                                                                                                                                                                                                  |
| Rate-tier + CSRF markers                                            | **Faithful seam**                         | `D2GeneratedRateLimitTier` / `D2GeneratedCsrfPosture` — markers are asserted PRESENT on endpoint metadata; no rate-limit or CSRF enforcement (unbuilt Edge middleware; replace-trigger: Edge rate-limit/CSRF middleware landing)                                                                                                                                 |
| D2Result → IResult (MAP-ii)                                         | **Real**                                  | Status-authoritative: `(int)result.StatusCode < 400` → `Results.Json(result.Data, statusCode: status)`; ≥400 → `ToProblemDetails(HttpContext)` from `D2.Shared.Auth.Http.ProblemDetails` (preserves `d2_error_code`). Keying on status (not `result.Success`) ensures `SomeFound` (206, `Success==false`) does not throw via the failure-only `ToProblemDetails` |

| Test class                    | Scenario                                                    | Key assertion                                                                                    |
| ----------------------------- | ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `RoutePolicyEnforcementTests` | Sign route + bearer with `self.write`                       | 200 OK (delegates to fake façade)                                                                |
| `RoutePolicyEnforcementTests` | Sign route + bearer with wrong scope                        | 401 `AUTH_SCOPE_INSUFFICIENT`                                                                    |
| `RoutePolicyEnforcementTests` | Sign route + bearer with no scopes                          | 401 `AUTH_SCOPE_INSUFFICIENT`                                                                    |
| `RoutePolicyEnforcementTests` | Sign route + no bearer                                      | 401 `AUTH_BEARER_MISSING`                                                                        |
| `RoutePolicyEnforcementTests` | AllScopes route + bearer with both `self.read`+`self.write` | 200 OK                                                                                           |
| `RoutePolicyEnforcementTests` | AllScopes route + bearer missing one scope                  | 401 `AUTH_SCOPE_INSUFFICIENT`                                                                    |
| `RoutePolicyEnforcementTests` | AllScopes route + no bearer                                 | 401 `AUTH_BEARER_MISSING`                                                                        |
| `RoutePolicyEnforcementTests` | Fake façade returns `ServiceUnavailable`                    | 503 `application/problem+json`                                                                   |
| `RouteFacadeDelegationTests`  | Sign route success → façade received correct input          | `fake.SignCallCount == 1`, `fake.LastSignFixtureInput.Kid == kid`                                       |
| `RouteFacadeDelegationTests`  | Sign route → `ServiceUnavailable`                           | 503 `application/problem+json`                                                                   |
| `RouteFacadeDelegationTests`  | Sign route → `NotFound`                                     | 404 `application/problem+json`                                                                   |
| `RouteFacadeDelegationTests`  | Sign route → `ValidationFailed`                             | 400 `application/problem+json`                                                                   |
| `RouteFacadeDelegationTests`  | Sign route → `Created`                                      | 201 with body (status-fidelity: real 2xx, not collapsed to 200)                                  |
| `RouteFacadeDelegationTests`  | Sign route → `SomeFound`                                    | 206 with body; NOT `application/problem+json` (pins the SomeFound 206 latent-bug fix — old branch threw on 206) |

**D2TSP004 / D2TSP005 / D2TSP006 assertion notes**: D2TSP004 and D2TSP005 are tested in `route-policy-emitter.test.ts` via the pure-fn `onError` callback (no TypeSpec host needed) and in `route-emit.integration.test.ts` via `hasError()`. D2TSP006 (`idempotent-requires-route`) is tested directly in `route-emit.direct.test.ts` (`$onEmit_routeEmitDirect_IdempotentWithoutRoute_D2TSP006`) and in `route-emit.integration.test.ts` (`routeEmitIntegration_IdempotentWithoutRoute_D2TSP006`); catalog registration and error severity are asserted in `tests/idempotency-gate-emitter.test.ts` (`D2TSP006_IdempotentRequiresRoute_DirectCatalogTest`). Severity is `"error"` in `src/lib.ts`; the per-code severity is asserted explicitly in `tests/lib.test.ts` (all-catalog guard).

---

## Idempotency gate emitter — seam ledger

The HTTP result-replay idempotency store is an unbuilt Edge concern. The emitter owns a faithful seam interface so validation is not blocked on the unbuilt consumer. Deferred wiring is tracked in `docs/v2/PHASE_3.md` §G (deferred-work wire-up ledger); this seam ledger is the tracking artifact for the emitter side.

| Seam                                                                                                               | Kind                                                                                                                                                                      | Consumer                                                                   | Replace-trigger                                                        |
| ------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| `D2GeneratedIdempotencyStore` (emitter-owned generated interface — `TryGetAsync<TStored>` + `StoreAsync<TStored>`) | **Faithful seam** — validates the real replay contract (store/lookup/TTL-expiry/result-replay) against an in-memory `FakeIdempotencyStore` with injectable `TimeProvider` | Unbuilt Edge `Idempotency.*` HTTP-idempotency middleware (tracked: `docs/v2/PHASE_3.md` §G) | That middleware lib ships and implements `D2GeneratedIdempotencyStore` |

The `D2GeneratedIdempotencyStore` prefix signals emitter ownership; the real Edge `IIdempotencyStore` carries the un-prefixed name when it ships (the `D2Generated` prefix reserves the collision-free namespace). The design mirrors the route emitter's `D2GeneratedRateLimitTier` / `D2GeneratedCsrfPosture` faithful-seam pattern.

---

## Idempotency gate emitter — C# validation table (`RouteIdempotencyGateTests`)

TestServer host with real `UseD2Auth()` + `FakeSignFixtureSignerFacade` + `FakeIdempotencyStore` (injectable `FakeTimeProvider` for deterministic TTL-expiry tests).

| Seam                                                     | Real or fake                                         | Notes                                                                                                                    |
| -------------------------------------------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `JwtAuthMiddleware` / `JwtValidator` / `RequireAnyScope` | **Real**                                             | Same pipeline as `RoutePolicyEnforcementTests` — JWT, scope enforcement, MAP-ii                                          |
| `ISignFixtureSignerFacade`                              | **Fake** (`FakeSignFixtureSignerFacade`)            | Records `SignCallCount` + `SignDerivedCallCount`; returns canned `D2Result`                                              |
| `D2GeneratedIdempotencyStore`                            | **Faithful in-memory seam** (`FakeIdempotencyStore`) | Real TTL-expiry via injectable `TimeProvider` (`FakeTimeProvider`); real store/lookup; `ServiceUnavailable` when faulted |

| Test                                                                         | Scenario                  | Key assertion                                                                                                                                                 |
| ---------------------------------------------------------------------------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SignRoute_DuplicateIdempotencyKey_ReturnsStoredResult_WithoutCallingFacade` | Dup key (header)          | 2nd request replays stored result; `SignCallCount == 1`; `TryGetCallCount == 2`                                                                               |
| `SignRoute_MissingIdempotencyKeyHeader_Returns400ValidationFailed`           | Missing header            | 400 `application/problem+json`; `SignCallCount == 0`                                                                                                          |
| `SignRoute_WhitespaceIdempotencyKey_Returns400ValidationFailed`              | Whitespace-only header    | 400; `SignCallCount == 0` (Falsey guard)                                                                                                                      |
| `SignRoute_StoreReadOutage_FailsOpen_DelegateStillInvoked`                   | Store read outage         | Gate fails-open; 200 OK; `SignCallCount == 1`                                                                                                                 |
| `SignRoute_StoredFailure_ReplaysFailureOnSecondCall`                         | Stored failure            | First call stores 503; second call replays 503 without re-invoking façade                                                                                     |
| `SignDerivedRoute_DuplicateKid_ReturnsStoredResult_WithoutCallingFacade`     | Derived dup (same kid)    | 2nd request replays; key = `SHA-256(kid)`; `SignDerivedCallCount == 1`                                                                                        |
| `SignDerivedRoute_DifferentKid_NoCacheHit_FacadeCalledAgain`                 | Derived different kids    | 2 distinct keys; `SignDerivedCallCount == 2`                                                                                                                  |
| `SignRoute_ExpiredIdempotencyKey_ReExecutes_AfterTtlElapses`                 | TTL-expiry (clock-driven) | Before expiry: replay, `SignCallCount == 1`. After `FakeTimeProvider.Advance(86401s)`: expired → miss → `SignCallCount == 2` (deterministic, no `Task.Delay`) |

---

## Server-push dispatch emitter — seam ledger

The `text/event-stream` channel gateway is an unbuilt Edge concern (the SSE wire binding is hand-written fringe per ADR-0021). The emitter owns a faithful seam family so the dispatch contract is validated without being blocked on the unbuilt consumer. The generated `<Op>Dispatcher` delivers a TYPED `<Op>Output` payload to the sink; serialization + the `data:`/`event:` framing are the sink's job.

| Seam                                                                                                                                                                                                       | Kind                                                                                                                                                                                                                                                            | Consumer                                                                                            | Replace-trigger                                                          |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `D2GeneratedSseEmitSink` (emitter-owned generated interface — generic `EmitAsync<TPayload>` over `D2GeneratedSseChannelTarget` { `D2GeneratedSseChannelClass` Class, string Id } + an event-type + payload) | **Faithful seam** — validates the real dispatch contract (channel class + targetId + event-type + payload round-trip; sink-failure propagation) against an in-memory `FakeSseEmitSink` that records all four and returns a configurable `D2Result` (non-vacuous) | Unbuilt Edge SSE channel gateway (the `text/event-stream` wire binding — hand-written per ADR-0021) | The Edge channel-gateway ships and implements `D2GeneratedSseEmitSink` |

The `D2GeneratedSseEmitSink` / `D2GeneratedSseChannelTarget` / `D2GeneratedSseChannelClass` prefix signals emitter ownership; the real Edge channel gateway carries the un-prefixed `ISseEmitSink` / channel vocabulary when it ships (the `D2Generated` prefix reserves the collision-free namespace). The design mirrors the idempotency emitter's `D2GeneratedIdempotencyStore` emitter-owned faithful-seam pattern.

---

## Server-push dispatch emitter — C# validation table (`SseDispatcherTests`)

Standalone (host-independent) — the generated dispatcher + seam compile into `D2.Edge.Tests`; no TestServer. Each test drives a generated `<Op>Dispatcher` directly with the faithful `FakeSseEmitSink`, or resolves the generated DI extension.

| Seam                      | Real or fake                                  | Notes                                                                                                              |
| ------------------------- | --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `D2GeneratedSseEmitSink`  | **Faithful in-memory seam** (`FakeSseEmitSink`) | Records `LastTarget` (class + id), `LastEventType`, `LastPayload`, `CallCount`; returns a configurable `D2Result` |
| `IServiceCollection` DI   | **Real**                                       | `AddD2PushFixturesSseDispatchers()` + `GetRequiredService<I<Op>Dispatcher>()` — descriptor presence ≠ resolvability |

| Test                                                                  | Scenario                  | Key assertion                                                                                                          |
| --------------------------------------------------------------------- | ------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `OrderShippedDispatcher_AddressesUserChannel_ForwardsAllFields`       | User-channel push         | sink sees `Class == User`, `Id == targetId`, `eventType == "orderShipped"`, the same payload instance (non-vacuous)   |
| `SessionExpiringDispatcher_AddressesSessionChannel_ForwardsAllFields` | Session-channel push      | sink sees `Class == Session`, `eventType == "sessionExpiring"` (the baked-in channel-class arm is non-vacuous vs User) |
| `OrderShippedDispatcher_SinkFailure_PropagatesServiceUnavailable`     | Sink outage               | the sink's `ServiceUnavailable` rides through verbatim — `Success == false`, never swallowed to `Ok` (§9.20)          |
| `OrderShippedDispatcher_AdversarialTargetId_ForwardedUnchanged`       | Empty / whitespace id     | the id rides through verbatim (`""` / `"   "`) — the dispatcher is a thin forwarder; the sink/gateway validate         |
| `AddD2PushFixturesSseDispatchers_ResolvesEachDispatcherToConcreteType`| DI resolution (§1.3)      | `GetRequiredService<IOrderShippedDispatcher>()` / `<ISessionExpiringDispatcher>()` resolve AS the generated impls      |
| `AddD2PushFixturesSseDispatchers_RegistersDispatchersTransient`       | DI lifetime               | two resolutions yield distinct instances (Transient registration is real)                                            |

---

## Server-push dispatch emitter — byte-parity table

Every committed `.g.cs` under `TypeSpecSse/Generated/` is re-emitted via the pure fns and asserted byte-identical, each with a deliberate-drift negative (`tests/sse-dispatch-emitter.test.ts`). The fixture push ops are PURE-push (param-less), so the emitter produces NO `I<Op>Handler` (suppressed by `isPurePush` — a pure-push op is a caller, not a request server) and NO `<Op>Input` (input DTO suppressed for pure-push ops — `dtoInputModel = undefined` for `isPurePush` ops, so no orphan parameterless input record is emitted; the byte-gate asserts `committed("OrderShippedInput.g.cs")` THROWS). The dispatch wiring (`$onEmit` push collection + the after-walk per-module DI loop + the once-per-namespace seam loop) is proven in `tests/sse-dispatch-emit.integration.test.ts` (real test-host compile — which also asserts NO `I<Op>Handler` and NO `<Op>Input` is emitted for a pure-push op, the handler + input suppression-proof regressions) + `tests/sse-emit.direct.test.ts` (mocked-compiler `src` credit, incl. the void/empty-output → D2TSP008 + no-partial arm, the pure-push handler-suppression, and the SELECTIVE combined push + `@d2GrpcMethod` → handler-emitted branch).

Net committed set per pure-push op: output DTO + dispatcher pair + (shared) seam + DI. NO input DTO, NO handler, NO façade entry.

| Committed fixture                                                       | Emitter call                                                              | Key assertion / drift negative                                                                                       |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| `TypeSpecSse/Generated/D2GeneratedSseEmitSink.g.cs` (emitter-owned seam) | `emitSseEmitSinkSeam(NS, "…/server-push-shaped.tsp")`                     | byte-identical to the committed seam; drift `EmitAsync` → `EmitAsyncDRIFTED`                                         |
| `TypeSpecSse/Generated/IOrderShippedFixtureDispatcher.g.cs`                    | `emitSseDispatcher(orderShipped op)[0]`                                   | byte-identical; drift `DispatchAsync` → `DispatchAsyncDRIFTED`                                                       |
| `TypeSpecSse/Generated/OrderShippedDispatcher.g.cs`                     | `emitSseDispatcher(orderShipped op)[1]`                                   | byte-identical; drift `D2GeneratedSseChannelClass.User` → `…Session` (the baked channel class is load-bearing)       |
| `TypeSpecSse/Generated/ISessionExpiringFixtureDispatcher.g.cs`                 | `emitSseDispatcher(sessionExpiring op)[0]`                                | byte-identical                                                                                                       |
| `TypeSpecSse/Generated/SessionExpiringDispatcher.g.cs`                  | `emitSseDispatcher(sessionExpiring op)[1]`                               | byte-identical; drift `…Session` → `…User` (the Session arm is non-vacuous vs User)                                  |
| `TypeSpecSse/Generated/PushFixturesSseDispatchersGenerated.g.cs`        | `emitSseDispatchersDiExtension("PushFixtures", [both ops], NS, "…")`     | byte-identical; drift `AddTransient` → `AddScoped` (the Transient lifetime is load-bearing)                          |
| `TypeSpecSse/Generated/OrderShippedFixtureOutput.g.cs` + nested `OrderLine`   | `emitCsharpDtos("orderShipped", NS, …, [], outputFields, [OrderLine])[1]` | byte-identical (the Output payload proves walkModel nested + temporal flow); drift `DateTimeOffset ShippedAt` → `…DRIFTED` |
| `TypeSpecSse/Generated/SessionExpiringFixtureOutput.g.cs`                      | `emitCsharpDtos("sessionExpiring", NS, …, [], outputFields, [])[1]`       | byte-identical (the Output payload with temporal field)                                                              |
| _(no `<Op>Input.g.cs`)_                                                 | _(input DTO suppressed for pure-push ops — `isPurePush` → `dtoInputModel = undefined`)_ | byte-gate asserts `committed("OrderShippedInput.g.cs")` THROWS (file absent from committed set)               |

**D2TSP008 assertion notes**: `server-push-requires-payload` is tested directly in `sse-emit.direct.test.ts` (`$onEmit_sseDirect_VoidOutput_D2TSP008` — void output → diagnostic fired + no partial dispatcher) and in `sse-dispatch-emit.integration.test.ts` (void-output AND empty-record-output arms, via the real test-host); catalog registration + error severity are asserted in `tests/lib.test.ts` (`lib_ServerPushRequiresPayloadPresent` + the all-catalog guard). Severity is `"error"` in `src/lib.ts`.

---

## REST route+policy emitter — byte-parity table

| Committed fixture                                                                                                          | Emitter call                                                                                                       | Key assertion                                                                                                                                                                                                              | Test file                                | Test name                                                                                                                                                      |
| -------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TypeSpecRoute/Generated/SignFixtureRouteRegistration.g.cs` (also contains marker records)                                        | `emitRoutePolicy({ opName: "sign", verb: "post", ... })`                                                           | Byte-identical to `SIGN_ROUTE_REGISTRATION_FIXTURE`                                                                                                                                                                        | `tests/route-policy-emitter.test.ts`     | `byteParity_SignRouteRegistration_CommittedFixtureIdentical > regenerated SignFixtureRouteRegistration.g.cs is byte-identical to the committed fixture`               |
| `TypeSpecRoute/Generated/AllScopesRouteRegistration.g.cs`                                                                  | `emitRoutePolicy({ opName: "allScopes", verb: "get", ... })`                                                       | Byte-identical to `ALL_SCOPES_ROUTE_REGISTRATION_FIXTURE`; verb is GET (emits `[AsParameters]` binding)                                                                                                                    | `tests/route-policy-emitter.test.ts`     | `byteParity_AllScopesRouteRegistration_CommittedFixtureIdentical > regenerated AllScopesRouteRegistration.g.cs is byte-identical to committed fixture`         |
| `TypeSpecRoute/Generated/SignFixtureRouteRegistration.g.cs` (header-gated form — `@d2Idempotent("header", 86400)`)                | `emitRoutePolicy({ ..., idempotency: { keySource: "header", ttlSeconds: 86400, fields: [] } })`                    | Byte-identical to `SIGN_ROUTE_REGISTRATION_GATED_FIXTURE` (gate woven in: `D2GeneratedIdempotencyStore store` param, header read, `Falsey()` guard, `TryGetAsync` replay, `StoreAsync` with `TimeSpan.FromSeconds(86400)`) | `tests/idempotency-gate-emitter.test.ts` | `byteParity_SignRouteRegistration_Gated_CommittedFixtureIdentical > regenerated SignFixtureRouteRegistration.g.cs (gated) is byte-identical to the committed fixture` |
| `TypeSpecRoute/Generated/SignFixtureDerivedRouteRegistration.g.cs` (derived-gated form — `@d2Idempotent("derived", 3600, "kid")`) | `emitRoutePolicy({ ..., idempotency: { keySource: "derived", ttlSeconds: 3600, fields: ["Kid"] } })`               | Byte-identical to `SIGN_DERIVED_ROUTE_REGISTRATION_FIXTURE` (SHA-256 derived key over `input.Kid`, `TimeSpan.FromSeconds(3600)`, no Falsey guard)                                                                          | `tests/idempotency-gate-emitter.test.ts` | `byteParity_SignDerivedRouteRegistration_CommittedFixtureIdentical > regenerated SignFixtureDerivedRouteRegistration.g.cs is byte-identical to the committed fixture` |
| `TypeSpecRoute/Generated/D2GeneratedIdempotencyStore.g.cs` (emitter-owned seam)                                            | `emitIdempotencyStoreSeam("D2.Edge.Tests.TypeSpecRoute.Generated", "contracts/typespec/fixtures/sign-shaped.tsp")` | Byte-identical to `IDEMPOTENCY_STORE_SEAM_FIXTURE` (public interface with `TryGetAsync<TStored>` + `StoreAsync<TStored>`, XML docs, auto-generated banner)                                                                 | `tests/idempotency-gate-emitter.test.ts` | `byteParity_IIdempotencyStore_SeamFixtureIdentical > regenerated D2GeneratedIdempotencyStore.g.cs is byte-identical to the committed fixture`                  |

**Deliberate-drift non-vacuity guards**: each byte-parity describe block contains a second test that mutates the fixture by one token and asserts the regenerated content does NOT match. `SignRouteRegistration`: `.replace("MapPost", "MapGet")`; `AllScopesRouteRegistration`: `.replace("RequireAllScopes", "RequireAnyScope")`; `SignRouteRegistration` (gated): `.replace("TryGetAsync", "TryGetAsyncDRIFTED")`; `SignDerivedRouteRegistration`: `.replace("SHA256.HashData", "SHA256.HashDataDRIFTED")`; `D2GeneratedIdempotencyStore`: `.replace("TryGetAsync", "TryGetAsyncDRIFTED")`.

---

## gRPC harness C# validation table

Committed C# fixture files exercised by the in-memory gRPC harness in `D2.Edge.Tests`:

| Fixture                             | Validated by                                                                                                                          | Key assertion                                                                                                                                                                                                                                                    |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `sign_fixture_signer_sign_fixture.g.proto` | `ProtoRoundTripTests` (7 tests)                                                                                                       | `SignRequest` / `SignResponse` proto3 messages compile and round-trip via `Grpc.Tools`-generated types; includes `ValidationFailed_D2Result_ToProtoAndBack_PreservesAllFields` and `Ok_D2Result_ToProtoAndBack_PreservesSuccessAndData` envelope-fidelity proofs |
| `SignFixtureSignerService.g.cs`    | `GrpcServiceImplTests.Sign_Success_ReturnsEnvelopeWithSuccessAndData`                                                                 | Proto→DTO mapping, façade delegation (`ISignFixtureSignerFacade.SignAsync`), and DTO→proto mapping work end-to-end via in-process `TestServer`; response carries `D2ResultProto` envelope with `Success=true` + typed data                                      |
| `SignFixtureSignerService.g.cs`    | `GrpcServiceImplTests.Sign_FacadeFailure_ValidationFailed_ReturnsEnvelopeWithRealCode` (+ `NotFound` + `ServiceUnavailable` variants) | `D2Result` business failures from the façade ride the `D2ResultProto` envelope with their real HTTP status code; gRPC status stays `OK` — no `RpcException` thrown                                                                                               |
| `SignFixtureSignerService.g.cs`    | `GrpcServiceImplTests.Sign_DelegatesThroughFacade_RecordsCallCount`                                                                   | Service calls the façade (not the handler) — call count asserted, proving the delegation target is the fixture façade                                                                                                                                            |
| `SignFixtureTransportMappers.g.cs`         | Exercised by `GrpcServiceImplTests` via `SignFixtureSignerService.g.cs`                                                              | `ToSignFixtureInput()` / `ToProtoSignFixtureOutput()` C# 14 extension members compile and map correctly; mapper is unchanged by the delegation target                                                                                                                          |

---

## gRPC CLIENT emitter — captured-envelope behavioral validation table (`GrpcClientTests`)

The generated `sign` gRPC CLIENT (`SignFixtureGrpcClient.g.cs`) is validated against its REAL seams — no test-doubles where a real seam exists. The linchpin the client pins is the **captured-envelope discipline**: a throwing transport fault re-enters the retry layer, while a callee's returned `D2Result` rides the `D2ResultProto` envelope with gRPC status `OK` so the transport sees SUCCESS and never auto-retries it (no n×m amplification). 16 `[Fact]` cases in `server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/GrpcClientTests.cs`.

| Seam                                                                                                                                                                                                                                                                     | Real or fake                                           | Notes                                                                                                                                              |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2.Shared.Resilience` keyed pipeline (`ResilientPipeline<string, SignFixtureOutput?>` + `AddResilientPipeline` + `RetryOptions.IsTransient`)                                                                                                                                   | **Real**                                               | The generated client runs the stub call inside the REAL pipeline; the retry gate is the real `ProtoExtensions.IsTransientGrpcException` predicate  |
| `D2.Shared.Result.Grpc` envelope mapper (`ToProto` / `ToD2Result` / `ToTransportFaultResult` / `IsTransientGrpcException`)                                                                                                                                               | **Real**                                               | Business failures reconstruct via the real `envelope.ToD2Result(...)`; transport faults map via the real `ToTransportFaultResult` (→ 503, not 500) |
| In-memory gRPC `TestServer` channel (`UseTestServer()` + `GrpcChannel.ForAddress(... HttpClient = host.GetTestClient())`)                                                                                                                                                | **Real in-memory transport**                           | No sockets; exercises the genuine gRPC stub call path                                                                                              |
| Fault-injecting `SignFixtureSigner.SignFixtureSignerBase` shims (`ThrowingSignerBase`, `FlakyThenSuccessSignerBase`, `BusinessFailureSignerBase`, `NullEnvelopeSignerBase`, `DelayThenSuccessSignerBase`, `EchoSignerBase`, `SuccessSignerBase`, `NullDataSignerBase`) | **Faithful test-only seam** (the one new shim)         | Concrete gRPC service subclasses that inject the fault/echo behavior; they call the REAL `D2Result`/envelope path                                  |
| Channel address + one-time outbound-auth registrations                                                                                                                                                                                                                   | **Host-gated replace-trigger** (NOT wired in the test) | See replace-triggers below — these are inert until a real Edge host wires them                                                                     |

| Linchpin pin / class                                   | Covered by case                                                                                    | Key assertion                                                                                                                                                                                                        |
| ------------------------------------------------------ | -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Business `ValidationFailed` → NOT retried**          | `SignAsync_BusinessFailure_ValidationFailed_ReturnsRealCode_NotRetried`                            | 400 survives full fidelity; `CallCount == 1` under a 5-attempt retry pipeline (the envelope rides gRPC status `OK`, so retry never sees a throw)                                                                     |
| **Transient transport fault → retried**                | `SignAsync_TransportTransient_RetriesThenServiceUnavailable`                                       | thrown `RpcException(Unavailable)` retried (`CallCount > 1`) then budget-exhaust → 503                                                                                                                               |
| **Transient transport fault → recovers**               | `SignAsync_TransportTransient_RecoversAfterRetry`                                                  | flaky-then-success recovers; `CallCount == 2`; real signature returned                                                                                                                                               |
| Envelope fidelity (status / category / error-code)     | `..._NotFound_ReturnsRealCode` / `..._ConflictWithErrorCode_SurvivesRoundTrip`                     | 404 round-trips; `KC_KEY_NOT_FOUND` error-code survives in the reconstructed envelope                                                                                                                                |
| Transport-fault → 503 (not 500)                        | `..._Unavailable_` / `..._DeadlineExceeded_` / `..._PermissionDenied_ReturnsServiceUnavailable`    | thrown `RpcException` → 503 via `ToTransportFaultResult` for transient + non-transient statuses                                                                                                                      |
| Permanent transport status not retried                 | `SignAsync_TransportPermanent_InvalidArgument_NotRetried_ServiceUnavailable`                       | code 3 ∉ `IsTransientGrpcException` → `CallCount == 1`, 503                                                                                                                                                          |
| Adversarial (null data / null envelope / cancellation) | `..._NullData_` / `..._NullEnvelope_DoesNotThrow_` / `..._Canceled_ReturnsNonSuccess_NotUnhandled` | void-output null data, malformed null `Result` on a 200, and caller-cancel all surface without an NRE / without collapsing to 500                                                                                    |
| Input round-trip (Kid + bytes Payload)                 | `SignAsync_InputRoundTrip_KidAndPayloadSurviveProtoMapping`                                        | Kid + Payload survive DTO→proto→DTO via the generated client mapper                                                                                                                                                  |
| Per-call pipeline override                             | `SignAsync_WithPassThroughOverride_BypassesInjectedRetryPipeline`                                  | a per-call `pipelineOverride` bypasses the injected retry; `CallCount == 1`                                                                                                                                          |
| **§1.3 DI resolution (every registered seam)**         | `AddD2SignFixtureGrpcClients_ResolvesClientAndKeyedPipeline`                                      | `GetRequiredService<ISignFixtureGrpcClient>()` resolves AS `SignFixtureGrpcClient` **and** `GetRequiredKeyedService<ResilientPipeline<…>>(SignClientKeys.PIPELINE)` resolves — descriptor-presence ≠ resolvability |

**Replace-triggers (host-gated wiring — inert until a real Edge host exists)**: the generated DI ext takes a required host-supplied channel address and auto-chains the per-channel `.AddD2ForwardedJwt().AddD2WorkloadCertificate()`, but the channel-address supply and the one-time outbound-auth composition-root registrations are NOT wired by the test. Both are tracked in `docs/v2/PHASE_3.md` §G (deferred-work wire-up ledger).

**Nested-model / array-of-model gRPC response fields**: the gRPC client + service transport mappers now recurse nested-model and array-of-model response fields to arbitrary depth — see the "Nested-model / array-of-model gRPC wire support" section below + roadmap §C row C18 (done).

---

## `@d2Resilience` predicate emitter — cross-language emission + sentinel retry

The `retryWhen` / `failWhen` custom predicates emit TWO behaviorally-identical predicate functions per op (one C#, one TypeScript) over the reconstructed business `D2Result<<Op>Output?>`, plus an emitter-owned retry sentinel the generated gRPC client throws to opt one named business condition into the keyed `ResilientPipeline`'s retry — with ZERO `D2.Shared.Resilience` change (the sentinel rides the existing `RetryOptions.IsTransient` extension point). Each `result.data.<path>` segment is resolved against the real output model at GENERATION time (no runtime reflection) → direct, type-safe, null-safe member access in both languages, full C#↔TS parity. The predicate-fixture op is `placeOrder` (`contracts/typespec/fixtures/resilience-predicate-shaped.tsp`), a flat-mappable rich shape (array `itemStatuses` + flat `partial` + scalars) carrying real registry literals (`infrastructure_unavailable` / `VALIDATION_FAILED`); the existing `sign` / `signWithKind` fixtures + their byte-gates stay byte-identical (a gRPC client without a predicate emits exactly the baseline client).

### Byte-parity table (`predicate-byte-parity.test.ts`)

Each row re-emits the artifact (the fixture model compiled through the test-host; the predicate / sentinel / client re-emitted via direct emitter calls) and asserts byte-identity to the committed fixture; each carries a deliberate-drift negative.

| Generated fixture                                                     | Re-emit source                                                                                                                                                                                       | Drift negative                                                                                                          |
| --------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `PlaceOrderResiliencePredicates.g.cs`                                 | `emitResultPredicates` (C# `SR_RetryWhen` / `SR_FailWhen` Func fields)                                                                                                                               | mutated `SR_RetryWhen` token ≠ re-emit                                                                                  |
| `place-order-fixture-resilience-predicates.g.ts`                              | `emitResultPredicates` (TS `placeOrderRetryWhen` / `placeOrderFailWhen`)                                                                                                                             | mutated `placeOrderRetryWhen` token ≠ re-emit                                                                           |
| `PlaceOrderV2ResiliencePredicates.g.cs` (NESTED + array-of-MODEL)     | `emitResultPredicates` over `placeOrderV2` (deep `?.`-chain `r.Data?.Customer?.Tier` + LINQ `r.Data?.Lines?.Any(l => l.Status …)`) — emitted STANDALONE (the predicate emission needs only the model + AST); the V2 gRPC client is committed separately via the nested-model wire support | mutated `Customer?.Tier` token ≠ re-emit (+ a non-vacuity assertion that the body carries the `?.`-chain + `.Any(...)`) |
| `place-order-v2-fixture-resilience-predicates.g.ts` (NESTED + array-of-MODEL) | `emitResultPredicates` over `placeOrderV2` (`r.data?.customer?.tier` + `r.data?.lines?.some((l) => l.status …)`)                                                                                     | mutated `customer?.tier` token ≠ re-emit (+ a non-vacuity `.some(...)` body assertion)                                  |
| `D2GeneratedBusinessRetrySignal.g.cs`                                 | `emitBusinessRetrySignal` (internal sealed `Exception`, carries the captured `D2ResultProto`)                                                                                                        | mutated class name ≠ re-emit                                                                                            |
| `PredicateFixturesGrpcClient.g.cs` + `…GrpcClientsGenerated.g.cs`     | `emitGrpcClient` (impl gains the sentinel-throw arm + budget-exhaust restore; DI-ext `IsTransient` gains the sentinel arm)                                                                           | mutated sentinel ref ≠ re-emit                                                                                          |

### Behavioral validation table (`PredicateRetryTests`, `D2.Edge.Tests`)

The generated predicate-bearing client is validated against its REAL seams — the `D2.Shared.Resilience` keyed pipeline + the real envelope mapper + an in-memory gRPC `TestServer` hosting fault-injecting `PredicateFixturesOrders.PredicateFixturesOrdersBase` shims. 8 `[Fact]` cases.

| Linchpin pin                                                             | Covered by case                                                              | Key assertion                                                                                                                                                          |
| ------------------------------------------------------------------------ | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **`retryWhen` retries a BUSINESS result + restores verbatim on exhaust** | `PlaceOrderAsync_RetryWhenMatches_RetriesThenRestoresBusinessResultVerbatim` | a `partial==true` success matches `retryWhen` (not `failWhen`) → `CallCount > 1`; on exhaust the 200 is restored VERBATIM (not 503 / 500) — FAILS WITHOUT the sentinel |
| **`retryWhen` recovers**                                                 | `PlaceOrderAsync_RetryWhenThenSuccess_RecoversAndReturnsSuccess`             | first attempt matches `retryWhen`, second does not → recovers; `CallCount == 2`                                                                                        |
| **`retryWhen` via the array accessor**                                   | `PlaceOrderAsync_RetryWhenArrayContains_RetriesViaGeneratedAccessor`         | a success whose `itemStatuses` contains `"PENDING"` drives the generated `Contains(...)` predicate → retry                                                             |
| **`failWhen` suppresses retry**                                          | `PlaceOrderAsync_FailWhenMatches_NotRetried_ReturnedVerbatim`                | a `VALIDATION_FAILED` result → `CallCount == 1`, returned verbatim (400)                                                                                               |
| **`failWhen` WINS over `retryWhen`**                                     | `PlaceOrderAsync_BothPredicatesMatch_FailWhenWins_NotRetried`                | a result matching BOTH → `CallCount == 1` (failWhen suppresses the retry)                                                                                              |
| **`failWhen` via empty collection**                                      | `PlaceOrderAsync_FailWhenEmptyCollection_NotRetried`                         | a success with zero `itemStatuses` → `failWhen` (`count == 0`) → `CallCount == 1`                                                                                      |
| **neither predicate matches**                                            | `PlaceOrderAsync_NeitherPredicateMatches_ReturnedVerbatim_NotRetried`        | a clean success → the default path, `CallCount == 1`, no spurious retry                                                                                                |
| **§1.3 DI resolution**                                                   | `AddD2PredicateFixturesGrpcClients_ResolvesClientAndKeyedPipeline`           | `GetRequiredService<IPredicateFixturesGrpcClient>()` + `GetRequiredKeyedService<ResilientPipeline<…>>(PlaceOrderClientKeys.PIPELINE)` resolve                          |

### Cross-runtime parity (`PredicateParityTests` + `predicate-parity.test.ts`)

Both languages drive the SAME shared matrices and assert the emitted C# predicate and the emitted TS predicate produce the SAME `retry` / `fail` booleans; the TS side drives the ACTUAL committed `.g.ts` bytes (the byte-gate pins them), so the parity test exercises exactly the emitter output, not a re-declaration.

- **Flat matrix** (`contracts/resilience/predicate-parity.fixture.json`, the `placeOrder` op): `retryWhen`-true / `failWhen`-true / `failWhen`-wins / array `.contains` / `.count`-on-empty (vacuous) / flat-bool / null-mid-path → false / unknown-category. Asserted NON-VACUOUS (both outcomes for both predicates, plus a both-true row).
- **Nested + array-of-MODEL matrix** (`contracts/resilience/predicate-parity-nested.fixture.json`, the `placeOrderV2` op): the BEHAVIORAL cross-language proof of the rich emission the flat matrix cannot exercise — each row EXECUTES the emitted deep `?.`-chain (`customer.tier`) + the array-of-MODEL quantifier (`lines.any(l => l.status …)`) in BOTH runtimes. Covers nested-path present-matching / present-non-matching / absent (deep `?.` → false), array `.any` true / false-with-elements / empty-vacuous, `.count == 0` failWhen, `failWhen`-wins, and null-data-never-throws. Asserted NON-VACUOUS with explicit guards that a row drives retry SOLELY via the nested path and another SOLELY via the array quantifier (so neither construct is vacuously satisfied). `placeOrderV2`'s predicate twin is itself emitted STANDALONE (the emitter crawls the output MODEL + AST at gen time — model + AST only, no gRPC transport mapper needed); the V2 gRPC client IS now committed via the nested-model wire support (§C row C18 is done — see the "Nested-model / array-of-model gRPC wire support" section).

---

## Nested-model / array-of-model gRPC wire support

The gRPC client + service transport mappers recurse nested-MODEL and array-of-MODEL response fields to ARBITRARY depth. A nested-model DTO field maps to a proto sub-message and an array-of-model field maps to a `repeated <Message>`; because a nested field's proto type differs from its DTO type, the value recurses through a per-nested-model SUB-MAPPER pair appended into the same mapper file — `extension(<Model> source) { internal Proto<Model> ToProto<Model>() … }` (DTO → proto) and `extension(Proto<Model> source) { internal <Model> To<Model>() … }` (proto → DTO). The recursion is depth-agnostic: `collectFieldNestedModels` (`src/lib/nested-model-mapper.ts`) walks the full transitive closure (deduped by model name, which also terminates a cycle), so a nested model that references a deeper model emits a sub-mapper for that one too. A `repeated <Message>` field uses the C# object-initializer collection form `Field = { source.X.Select(x => x.ToProto<Model>()) }` outbound (a `RepeatedField<T>` has no setter — items are ADDED) and `source.X.Select(x => x.To<Model>()).ToList()` inbound; a nullable single nested model is a bare proto3 message field (implicit presence) mapped `source.X is null ? null : source.X.To<Model>()` (absent → `null`, never a default instance). The committed fixtures are the V2 `placeOrderV2` module (optional nested `customer` + array-of-model `lines`) and the depth-3 `deepNest` module (`output → optional widget → parts[]`).

### Byte-parity table (`nested-model-grpc-byte-parity.test.ts`)

Each row compiles the predicate fixture (`resilience-predicate-shaped.tsp`) through the TypeSpec test-host to obtain the REAL output models, re-emits the artifact via the REAL emitters, and asserts byte-identity to the committed fixture; each describe carries a deliberate-drift negative (mutate one token → assert NOT byte-identical).

| Generated fixture                                                                                                        | Re-emit source                                                                                                                                | Drift negative                                                                            |
| ------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `predicate_fixtures_orders_v2_place_order_v2_fixture.g.proto` (V2 — `repeated PlaceOrderLine` + bare `PlaceOrderV2Customer`)      | `emitProto` over `placeOrderV2` (deduped nested models)                                                                                        | `message PlaceOrderLine` → `…DRIFTED`                                                      |
| `PlaceOrderV2ClientMappers.g.cs` (V2 client mappers — array-of-model + nullable-nested recursion + both sub-mappers)      | `emitGrpcClient` over `placeOrderV2` (mappers file)                                                                                            | `ToProtoPlaceOrderLine` → `…DRIFTED`                                                       |
| `IPredicateFixturesV2GrpcClient.g.cs` / `PredicateFixturesV2GrpcClient.g.cs` / `…GrpcClientsGenerated.g.cs`               | `emitGrpcClient` over `placeOrderV2` (interface / predicate-bearing impl / DI ext)                                                             | impl `ToPlaceOrderV2FixtureOutput` → `…DRIFTED`                                                   |
| `PlaceOrderV2ClientKeys.g.cs`                                                                                             | `emitClientKeys("placeOrderV2", …)`                                                                                                            | (covered by the module-file describe)                                                     |
| `DeepNestFixtureOutput.g.cs` + `deep-nest-fixture-dto.g.ts` (depth-3 — `DeepNestFixtureOutput → DeepWidget → DeepPart` at every level)          | `emitCsharpDtos` / `emitTsDtos` over `deepNest` (deduped nested models)                                                                        | `DeepPart` → `DeepPartDRIFTED`                                                             |
| `predicate_fixtures_gizmos_deep_deep_nest_fixture.g.proto` (a message at EVERY depth + `repeated DeepPart` inside `DeepWidget`)   | `emitProto` over `deepNest`                                                                                                                    | `message DeepPart` → `…DRIFTED`                                                            |
| `DeepNestClientMappers.g.cs` (a sub-mapper for EVERY nested level + the depth-3 nested-array recursion)                   | `emitGrpcClient` over `deepNest` (mappers file)                                                                                                | `ToProtoDeepPart` → `…DRIFTED`                                                             |
| `IPredicateFixturesDeepGrpcClient.g.cs` / `PredicateFixturesDeepGrpcClient.g.cs` / `…GrpcClientsGenerated.g.cs` / `DeepNestClientKeys.g.cs` | `emitGrpcClient` + `emitClientKeys` over `deepNest`                                                                                            | impl `ToDeepNestFixtureOutput` → `…DRIFTED`                                                       |

The SERVER transport mapper for `placeOrderV2` / `deepNest` is emitted and its nested recursion is unit-asserted (`emitGrpcService_*_ServerMapperRecursion_NonCommitted` re-emits and pins the collection-init array-of-model + nullable-nested + per-level sub-mappers), but it is NOT committed as a separate `.g.cs`: the client mapper and the server mapper share the same namespace and emit the SAME per-nested-model sub-mappers (`ToProtoPlaceOrderLine` / `ToPlaceOrderLine` …), so compiling both in one assembly would collide (CS0121). The committed client mapper is the byte-gated proof; this matches V1's client-only commit.

### Behavioral round-trip table

The generated nested-model clients are validated against their REAL seams — the `D2.Shared.Resilience` keyed pipeline + the real envelope mapper + the REAL generated nested sub-mappers — over an in-memory gRPC `TestServer` + `GrpcChannel` (no sockets) hosting a concrete shim that builds the proto response tree directly. The CLIENT proto → DTO recursion is exercised end-to-end. `PlaceOrderV2RoundTripTests` (V2 — optional nested model + array-of-model) + `DeepNestRoundTripTests` (depth-3).

| Pin                                                       | Covered-by case                                                                  | Key assertion                                                                                                                                          |
| -------------------------------------------------------- | -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| R1 — populated nested model + multi-element array         | `PlaceOrderV2_NestedCustomerAndLinesArray_RoundTripFullFidelity`                  | `Customer.Tier == "GOLD"` + both `Lines` elements (`SHIPPED` / `DELIVERED`) survive proto ↔ DTO with full fidelity                                     |
| R2 — absent nullable nested model                         | `PlaceOrderV2_AbsentNullableCustomer_MapsToNull_NoNre`                            | proto3 implicit presence: an unset `customer` → `Data.Customer is null` (NOT a default instance), no NRE                                               |
| R3 — empty array-of-model                                 | `PlaceOrderV2_EmptyLinesArray_MapsToEmptyList_NoNre`                              | an empty `lines` → `Data.Lines` empty + non-null, no NRE                                                                                               |
| R4 — flat input round-trip                                | `PlaceOrderV2_FlatRequest_CustomerIdSurvivesProtoMapping`                         | the request mapper sent `customerId` over the wire (`shim.LastCustomerId == "cust-echo-42"`)                                                           |
| R5 — predicate over the now-wire-mappable nested output   | `PlaceOrderV2_NestedTrialTier_DrivesPredicateRetry_RestoredVerbatim` (+ `…_NestedLinePending_DrivesPredicateRetryViaArrayQuantifier`) | nested `customer.tier=="TRIAL"` (and a `PENDING` line via the array quantifier) matches `retryWhen` over the wire-mapped output → `CallCount > 1`, the 200 restored VERBATIM on exhaust |
| R6 — §1.3 DI resolution (V2)                              | `AddD2PredicateFixturesV2GrpcClients_ResolvesClientAndKeyedPipeline`             | `GetRequiredService<IPredicateFixturesV2GrpcClient>()` + `GetRequiredKeyedService<ResilientPipeline<…>>(PlaceOrderV2ClientKeys.PIPELINE)` resolve      |
| Adversarial — business failure rides the envelope         | `PlaceOrderV2_BusinessFailure_RidesEnvelope_NotRetried_NullData`                  | a `VALIDATION_FAILED` result → `CallCount == 1` (failWhen matched), 400, real error-code, `Data` null                                                  |
| Depth-3 — nested widget + nested array survive            | `DeepNest_Depth3_NestedWidgetAndPartsArray_RoundTripFullFidelity`                 | `output → Widget (name) → Parts[]` (`P-1` / `P-2`, the array-of-MODEL INSIDE a nested model) survive proto ↔ DTO at depth 3                            |
| Depth-3 — absent depth-2 nested model                     | `DeepNest_AbsentNullableWidget_MapsToNull_NoNre`                                  | an unset `widget` → `Data.Widget is null`, no NRE                                                                                                      |
| Depth-3 — empty depth-3 array                             | `DeepNest_EmptyDepth3PartsArray_MapsToEmptyList_NoNre`                            | a widget with an empty `parts` → `Parts` empty + non-null, no NRE                                                                                      |
| Depth-3 — §1.3 DI resolution (Deep)                       | `AddD2PredicateFixturesDeepGrpcClients_ResolvesClientAndKeyedPipeline`           | `GetRequiredService<IPredicateFixturesDeepGrpcClient>()` + `GetRequiredKeyedService<ResilientPipeline<…>>(DeepNestClientKeys.PIPELINE)` resolve        |

---

## TS client emitters — browser REST + server SSR gRPC

The TS-client emitters emit, per `@d2ServedBy` module, the consumer-facing client surfaces per the client-surface taxonomy (ADR-0021): a `<module>-grpc-client.g.ts` (SvelteKit SSR → gRPC, the module's `@d2GrpcMethod` ops) and a `<module>-rest-client.g.ts` (browser → REST, the module's `@route` ops). An op carrying BOTH `@route` AND `@d2GrpcMethod` (e.g. `sign`) appears in BOTH surfaces; the op-sets diverge by transport. Per-op typed fns, not a unified client.

- The **SSR gRPC client** is the TS twin of the .NET `<Module>GrpcClient`. It delegates to the REAL `@d2/grpc-client` seam (`unaryCall` / `handleGrpcCall` / `d2ResultFromProto` / `isTransientGrpcError`) over the REAL ts-proto grpc-js stub. For a `@d2Resilience` op it folds the emitted TS `<op>RetryWhen` / `<op>FailWhen` twin into a retry-arm over the EXISTING `@d2/resilience` `ResilientPipeline` (a module-local `D2GeneratedBusinessRetrySignal` carries the captured business `D2Result` for the budget-exhaust restore) — NO new resilience-lib export; transport-fault retry reuses `isTransientGrpcError`. A RESPONSE enum carries a fail-loud inbound membership parse (the TS twin of the C# strict `Parse<Enum>Wire`).
- The **browser REST client** delegates to the REAL `$lib` substrate — `apiCall` (scoped ops) / `apiCallAnon` (harmless ops) — which owns auth (JWT, retry-once-on-401), the locale/fingerprint headers, `fetch`, and ProblemDetails/envelope → `D2Result`. A `@d2Idempotent("header", …)` op threads `opts.idempotencyKey`; a `derived` keySource is server-computed (no client key). POST/PUT/PATCH ride the body; GET/DELETE bind the input fields as a query string.

Both emitted `.g.ts` carry `// @ts-nocheck` + `/* eslint-disable */` (they reference module-relative imports — the proto stub + DTOs + predicate twin for gRPC; the `$lib` alias for REST — that wire up only in the real BFF consumer); the byte-gate pins the exact bytes and the behavioral tests drive the ACTUAL emitted bytes (transpile + `new Function`).

### Real buf/ts-proto pipeline (the load-bearing claim)

The SSR gRPC client validation RUNS THE REAL buf/ts-proto toolchain (the `@d2/protos` package's `@bufbuild/buf` + `protoc-gen-ts_proto` + its committed `buf.gen.yaml` opt set, with `Mcommon/v1/d2_result.proto=@d2/protos` redirecting the common-import to the shipped `@d2/protos` package) on the committed fixture `.proto` → REAL fixture ts-proto types (the TS twin of Grpc.Tools — a grpc-js callback-style `<Service>Client` + `<Method>Request`/`<Method>Response` messages). The emitted client compiles + behaviorally-validates against THOSE real types over the REAL seam + a fake grpc-js stub. The committed fixture proto-TS (`tests/grpc-fixtures/generated/*.ts`) is re-generated by the test and asserted byte-identical (`tsGrpcClient_FixtureProtoByteGate` for `place_order.ts`; `tsGrpcClient_RealBufTsProtoSignPipelineByteGate` for `sign.ts`); it is NOT a type-double, NOT `@ts-nocheck`-opaque, NOT out-of-scope. (`contracts/protos/` is never polluted — the fixture protos + their generated TS live in the emitter test tree.)

#### Buf/ts-proto byte-gate ledger

| Committed fixture | Regeneration mechanism | Key assertion | Test | Replace-trigger |
| --------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------- | --------------------------------------- | --------------------------------------------------------------------------------------- |
| `tests/grpc-fixtures/generated/sign.ts` (ts-proto grpc-js stub — `SignFixtureSignerClient` + `SignRequest`/`SignResponse` messages) | The test invokes the real `@bufbuild/buf` CLI + `protoc-gen-ts_proto` on the committed `tests/grpc-fixtures/sign_fixture_signer_sign_fixture.g.proto` with the committed `buf.gen.yaml` opt set | Regenerated bytes are byte-identical to the committed `sign.ts` (`tsGrpcClient_RealBufTsProtoSignPipelineByteGate`) | `tests/ts-grpc-client-emitter.test.ts` | `buf.gen.yaml` opt-set changes; `protoc-gen-ts_proto` major-version bump; `@d2/protos` common-import redirect change |

### Byte-parity table (`ts-client-byte-parity.test.ts`)

Each row re-emits the artifact via the pure emitter fn and asserts byte-identity to the committed `.g.ts`; each carries a deliberate-drift negative (mutate one token → assert NOT byte-identical).

| Committed fixture                                                             | Emitter call                                                            | Drift negative                            |
| ---------------------------------------------------------------------------- | ---------------------------------------------------------------------- | ----------------------------------------- |
| `TypeSpecGrpcPredicate/Generated/predicate-fixtures-grpc-client.g.ts` (predicate retry-arm) | `emitTsGrpcClient("PredicateFixtures", [placeOrder])`                  | `placeOrderRetryWhen` → `…DRIFTED`        |
| `TypeSpecDto/Generated/sign-fixture-grpc-client.g.ts` (non-predicate, bytes) | `emitTsGrpcClient("KeyCustodian", [sign])`                            | `handleGrpcCall` → `…DRIFTED`             |
| `TypeSpecGrpcEnum/Generated/enum-fixtures-grpc-client.g.ts` (response enum)   | `emitTsGrpcClient("EnumFixtures", [signWithKind])`                    | `validationFailed` → `…DRIFTED`           |
| `TypeSpecDto/Generated/sign-fixture-rest-client.g.ts` (header + derived idempotency) | `emitTsRestClient("KeyCustodian", [sign, signDerived])`              | `apiCall` → `…DRIFTED`                     |

### SSR gRPC client — behavioral validation table (`ts-grpc-client-emitter.test.ts`, real seam + fake stub)

The emitted client (reconstructed from the committed bytes) is driven against the REAL `@d2/grpc-client` seam + the REAL `@d2/resilience` pipeline + the REAL fixture ts-proto types + a typed fake grpc-js stub.

| Pin                                                | Key assertion                                                                                                                            |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Success — proto data → DTO round-trip              | every field round-trips; the DTO→proto request mapper copied the field; `CallCount == 1`                                                |
| Business failure rides the OK envelope             | reconstructed via the real `d2ResultFromProto`; status/errorCode survive; NOT retried (`CallCount == 1`)                                |
| Business `retryWhen` → retried + recovers          | a `category infrastructure_unavailable` failure matches `retryWhen` → `CallCount > 1`, recovers on a later success                      |
| `failWhen` WINS over `retryWhen`                   | a `VALIDATION_FAILED` result matching BOTH → terminal (`CallCount == 1`)                                                                |
| `retryWhen` budget-exhaust → restore verbatim      | a `partial==true` success always matching `retryWhen` → 3 calls (maxAttempts), the captured 206 restored VERBATIM (not 500)            |
| Transport transient (UNAVAILABLE) → retried        | classified by the real `isTransientGrpcError` → `CallCount > 1`, recovers                                                               |
| Terminal transport fault → 503, NEVER leaks detail | a non-transient `NOT_FOUND` → mapped via the seam to 503; the raw `broker://secret@host` detail never reaches `result` (§3.1)           |
| Caller cancellation (CANCELLED)                    | mapped to the seam's `canceled` (errorCode `CANCELED`); the abort detail never leaks                                                    |
| `deadlineMs` threaded to `unaryCall`               | the deadline reaches grpc-js via the (request, Metadata, CallOptions, cb) overload                                                      |
| Per-call pipeline override                         | a caller-supplied `ResilientPipeline.PassThrough` replaces the default retry pipeline                                                   |
| Response enum — known wire value                   | maps back to the DTO union member (success); the DTO enum value (the wire string) copied straight onto the proto request               |
| Response enum — unknown wire value                 | fail-loud client-side `ValidationFailed` (400), NO fallback                                                                             |
| §26.3.2 capability parity                          | table-driven: the TS client mirrors the .NET client's deadline / pipelineOverride / transient-retry / predicate-sentinel / business-gated / no-detail-leak capabilities |
| **Cross-runtime predicate consumption parity**     | over the SHARED `predicate-parity.fixture.json`, the TS client retries IFF `expectedRetry && !expectedFail` (the runtime rule) — matching the .NET client's consumption of the SAME predicate twin (`tsGrpcClient_CrossRuntimePredicateConsumptionParity`) |

### Browser REST client — behavioral validation table (`ts-rest-client-emitter.test.ts`, faithful `apiCall` double)

The real substrate is the dormant cross-workspace `server/web` BFF (the `$lib` alias resolves only inside SvelteKit; the real wiring is the host-gated Browser REST client wiring) — so the emitted REST client is driven against a FAITHFUL `apiCall`/`apiCallAnon` double (same signature, returns the real `@d2/result` `D2Result`, records path/method/body/idempotencyKey).

| Pin                                          | Key assertion                                                                                       |
| -------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Success — path + verb + body forwarded       | the double received the correct path (`/internal/v1/fixtures/sign-fixture`), `POST`, and the input as body        |
| Header-idempotent op threads the key         | `opts.idempotencyKey` → the substrate's `idempotencyKey`                                            |
| Derived-keySource op does NOT thread a key   | no client idempotency key (server-computed)                                                         |
| Failure forwarding (ValidationFailed / 503)  | the substrate failure is returned verbatim (no swallow into `ok()`, §9.20)                          |
| Harmless op → `apiCallAnon`                   | a harmless op delegates to the anonymous substrate (not `apiCall`)                                  |
| GET op → query binding, no body              | input fields → query string; no body                                                               |
| Adversarial — empty/whitespace input         | forwarded as-is (the client is a thin forwarder; the substrate + server validate)                  |
| Abort signal + timeout threaded              | `opts.signal` + `opts.timeout` → the substrate                                                      |

### `$onEmit` dispatch integration (`ts-client-emit.integration.test.ts`)

Compiles inline `.tsp` through the TypeSpec test-host and asserts the in-memory FS contains the emitted TS clients — proving the dispatch wiring (the `restOpsByModule` collection + the after-walk loops reusing `grpcOpsByModule`): a `@route`+`@d2GrpcMethod` op emits BOTH clients; a `@d2Resilience` gRPC op's TS client folds in the predicate retry-arm + emits the twin; a `@route`-only op emits the REST client but NO gRPC client; the bare-`retry()` budget defaults to 3.

### Host-gated BFF-wiring replace-triggers

Both seams exist, so both emitters are BUILT + validated now; the ONLY deferrals are the host-gated BFF composition-root WIRING (BFF-rebuild-gated). The canonical tracking is this ledger; both rows are in `docs/v2/PHASE_3.md` §G (SSR-gRPC-client → BFF gRPC composition root + browser-REST-client → BFF browser integration).

| Replace-trigger | Deferred wiring | Why deferred (genuine block) | Validated now against |
| --------------- | --------------- | ---------------------------- | --------------------- |
| **SSR gRPC client → BFF composition root** | The real SSR gRPC channel + BFF composition root (`getChannel(...)` → real Edge endpoint + the real ts-proto stub + the generated `<module>GrpcClient` wired in `server/web/src/lib/server/…` + the context-propagation interceptor + boundary-token cache) | The BFF SSR composition root does not exist (the v2 BFF rebuild is a downstream phase) | The REAL `@d2/grpc-client` seam + the REAL fixture ts-proto types (real buf/ts-proto) + a fake stub |
| **Browser REST client → BFF browser integration** | The real browser fetch-substrate wiring (`server/web/src/lib/client/rest/gateway-client.ts` `apiCall`/`apiCallAnon` wired to the generated `<module>RestClient`; the `$lib` alias resolves) | The substrate is the dormant pre-pivot BFF (not host-typechecked; the tracked `D2Result` static-call gap; `$lib` resolves only in `server/web`) | A FAITHFUL `apiCall`/`apiCallAnon` double (real signature, real `D2Result`) |

---

## OpenAPI `x-d2-*` extension emitter — real `getOpenAPI3` + `@d2*` stateMap seams

The OpenAPI emitter (`src/lib/openapi-emitter.ts`) produces one OpenAPI 3.0
document per `@service` namespace × version. The HTTP shape comes VERBATIM from
the **genuine stock `@typespec/openapi3` emitter** via its programmatic
`getOpenAPI3(program)` API — the emitter reimplements NO part of the OpenAPI
document (no paths / schemas / components / requestBody are built here). It
layers ONLY the four `x-d2-*` policy extensions that stock OpenAPI cannot
express, read directly from the `@d2*` decorator `stateMap`s (the same reads the
route-policy emitter performs), plus a document-level `x-d2-generated-by`
traceability marker.

| Seam                                                       | Real or fake | Notes                                                                                                                                                       |
| ---------------------------------------------------------- | ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@typespec/openapi3` `getOpenAPI3(program)`                | **Real**     | The genuine stock emitter produces the OpenAPI 3.0 document object(s) — `paths` / `components/schemas` / `requestBody` / `$ref` responses — NO reimplementation |
| `@typespec/http` `getAllHttpServices`                      | **Real**     | Builds the `(verb, path) → Operation` correlation index from the same HTTP-library walk the stock emitter uses, so the path keys align                       |
| `@typespec/compiler` `listServices`                        | **Real**     | The emit gate — `getOpenAPI3` runs only when ≥1 `@service` exists (also prevents the `no-service-found` HTTP warning on routed-but-serviceless programs)     |
| `@d2*` decorator `stateMap`s (any/all/harmless scope, tier, audience, csrf) | **Real**     | `x-d2-scope` / `x-d2-tier` / `x-d2-audience` / `x-d2-csrf` are read from the genuine decorator state — never a test double                                  |

| Concern                                                                | Test                                                                                                                                                   |
| ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| REAL stock OpenAPI 3.0 shape (paths + components + requestBody + `$ref`) | `openapi-emitter.test.ts > openApiEmitter_Integration_StockShapePlusExtensions > emits the genuine stock OpenAPI 3.0 shape …`                          |
| ALL FOUR `x-d2-*` present-and-correct on a fully-decorated op           | `… > injects ALL FOUR x-d2-* extensions, present-and-correct, on the fully-decorated op` (scope any + tier `Standard` + audience `d2-edge` + csrf `exempt`) |
| **ADVERSARIAL** — absence: op with only the required auth intent carries `x-d2-scope` but NOT tier/csrf/audience | `… > ADVERSARIAL: an op with only the required auth intent carries x-d2-scope but NOT tier/csrf/audience`                                              |
| `x-d2-scope` all-scopes arm `{ mode: "all", scopes }`                  | `… > encodes the all-scopes arm as x-d2-scope { mode: 'all', scopes }`                                                                                |
| `x-d2-scope` harmless arm `{ mode: "harmless" }`                       | `… > encodes the harmless arm as x-d2-scope { mode: 'harmless' }`                                                                                     |
| Doc-level `x-d2-generated-by` traceability marker                      | `… > emits a document-level x-d2-generated-by traceability marker`                                                                                    |
| **No PII leak** — no `@d2Redact` field VALUE / payload content in the doc | `… > NO PII LEAK: the emitted document never contains a @d2Redact field VALUE or payload content`                                                      |
| **Versioned fan-out is REAL** (one doc per version, non-vacuous)       | `openApiEmitter_Integration_VersionedFanOutIsReal > emits one file per (service × version): 1 unversioned + 2 versions = 3 documents`                  |
| **NON-VACUOUS version delta** — v1 vs v2 genuinely differ (`exportReport` `@added` in v2 only) | `… > NON-VACUOUS: v1 and v2 documents genuinely differ (exportReport is @added in v2 only)`                                                            |
| Each version document carries the `x-d2-*` extensions                  | `… > each version document carries the x-d2-* extensions on its ops`                                                                                  |
| `$onEmit` dispatch — the OpenAPI file is written through the emit loop  | `openapi-emit.direct.test.ts > openApiEmitDirect_OnEmitDispatch` (instrumented `src` `$onEmit`, real program)                                          |
| Pure `injectD2Extensions` branch coverage (scope arms + object-form tier/csrf + op-not-in-index skip) | `openapi-emitter.test.ts > openApiEmitter_Unit_injectD2Extensions` (synthetic documents + mocked `getAllHttpServices`)                                 |
| `emitOpenApiDocuments` record-arm handling (versioned / unversioned / empty-records / unnamed-service / empty-version) | `openapi-emitter.test.ts > openApiEmitter_Unit_emitOpenApiDocuments` (mocked `getOpenAPI3` + `listServices`)                                           |

**Byte-parity + non-vacuity** — `openapi-byte-parity.test.ts`:

| Committed fixture                                          | Emitter call                                                | Key assertion                                  |
| --------------------------------------------------------- | ---------------------------------------------------------- | ---------------------------------------------- |
| `TypeSpecOpenApi/Generated/open-api-fixtures.openapi.g.json`               | regenerate (compile `openapi-shaped.tsp` → `emitOpenApiDocuments`) | byte-identical to the committed fixture |
| `TypeSpecOpenApi/Generated/open-api-versioned-fixtures.1-0.openapi.g.json` | regenerate (versioned arm, v1)                              | byte-identical to the committed fixture         |
| `TypeSpecOpenApi/Generated/open-api-versioned-fixtures.2-0.openapi.g.json` | regenerate (versioned arm, v2)                              | byte-identical to the committed fixture         |

**Deliberate-drift non-vacuity guards**: two tests mutate a committed fixture by
one token and assert the regenerated output does NOT match — `open-api-fixtures`
mutates `self.write` → `self.writeDRIFTED` (inside `x-d2-scope`);
`open-api-versioned-fixtures.2-0` mutates `exportReport` → `exportReportDRIFTED`.
This proves the gate FAILS on real divergence (never a buffer-vs-itself
tautology). The committed `.g.json` documents are generated output: never
hand-edited (fixes land at the emitter), byte-gated, and `.prettierignore`d
(`**/*.g.json`) so Prettier never reformats them.

**Replace-trigger**: N/A — the emitted OpenAPI document is a build artifact with
no unbuilt runtime consumer (no Swagger UI / doc host wiring in scope). An Edge/BFF
OpenAPI consumer reads the `x-d2-*` extension shapes pinned here; no emitter change
is needed when that consumer ships.

---

## Wire identity — byte-parity table

| Committed fixture | Emitter call | Key assertion | Test file | Test name |
| --- | --- | --- | --- | --- |
| `TypeSpecGrpc/Generated/WireVersion.g.cs` | `emitWireVersionConstant(WIRE_NS, channel, WIRE_SOURCE)` where `channel = parseChannel("d2.signfixtures.v1")!` | Byte-identical to the committed fixture (CHANNEL/GENERATION/STABILITY fields agree with `proto-package`) | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_WireVersionConstant_CommittedFixtureIdentical` |
| `TypeSpecGrpc/Generated/wire-identity.manifest.g.json` | `emitWireIdentityManifest(protoPackage, protoCsharpNs, channel)` | Byte-identical to the committed fixture; all four identity facts present; `x-d2-generated-by` present; no package-name keys | `tests/proto-grpc-byte-parity.test.ts` | `byteParity_WireIdentityManifest_CommittedFixtureIdentical` |

**Deliberate-drift non-vacuity guard**: the `WireVersion.g.cs` byte-gate describe block contains a second test that emits with a mutated channel (`v3beta`) and asserts the output does NOT match the committed `v2alpha` fixture. This proves the gate is not a tautology comparing a buffer to itself.

**D2TSP010 non-vacuity**: `tests/wire-channel.test.ts` contains a NON-VACUOUS mismatch test that feeds `proto-package="d2.signfixtures.v1"` against `proto-csharp-namespace="D2.Services.Protos.KeyCustodian.V2Beta"` and asserts `onError` is called once with code `"channel-segment-mismatch"` and a message containing `"D2TSP010"`, `"v2alpha"`, and `"V2Beta"`. A matching pair also calls `onError` zero times. Both arms are required (vacuous fire-always and vacuous fire-never are equally broken gates).

**Integration tests**: `tests/proto-grpc-emit.integration.test.ts` adds four integration tests:
- `protoGrpcEmitIntegration_WireVersion_EmittedOnGrpcOp` — full `host.compile` with a `@d2GrpcMethod` op emits both `WireVersion.g.cs` (CHANNEL=`"v2alpha"`, GENERATION=2, STABILITY=`"alpha"`) and the manifest with all four identity facts.
- `protoGrpcEmitIntegration_D2TSP010_FiresOnChannelMismatch` — deliberately mismatched `proto-package` vs `proto-csharp-namespace` → error diagnostic with `d.message.includes("D2TSP010")`.
- `protoGrpcEmitIntegration_UnpinnedField_NoOrphanedWireIdentity` — unpinned proto field on the sole `@d2GrpcMethod` op → D2TSP009 fires; `WireVersion.g.cs` and `wire-identity.manifest.g.json` are NOT emitted (regression: a prior bug emitted them unconditionally even when the proto walk failed).
- `protoGrpcEmitIntegration_VersionedAdoption_ByteNeutralForExistingFixtures` — compile with `@versioned` KC inline → in-process handler file still emitted; no proto emitted; `WireVersion.g.cs` NOT emitted (no `@d2GrpcMethod` op).

---

## Byte-gate completeness sweep

EVERY committed generated file across the fleet — all `.g.cs` (DTO / handler /
service / transport-mapper / client-interface / client-impl / client-mapper / DI /
keys / predicate / idempotency / SSE / route-registration / wire-version subtrees), every `.g.ts`
DTO + client, every `.g.proto`, every `.openapi.g.json`, and every `.manifest.g.json` — has a re-emit
byte-identity gate (re-run the emitter → assert byte-identical to the committed
file) carrying a deliberate-drift negative. The gate set is the union of the
per-emitter byte-parity tables above; no committed generated file in the
`TypeSpecDto` / `TypeSpecGrpc` / `TypeSpecGrpcEnum` / `TypeSpecGrpcPredicate` /
`TypeSpecRoute` / `TypeSpecSse` / `TypeSpecOpenApi` subtrees is left without a gate.
A hand-edit to any generated file is therefore caught on the next test run (the
regen would diverge), enforcing the codegen discipline that fixes land at the
emitter, never the output.

**One excluded file — correctly, because it is NOT generated**:
`server/services/edge/tests/Unit/KeyCustodian/TypeSpecRoute/Generated/ISignFixtureSignerFacade.cs`
is a **hand-authored test-seam interface**, not emitter output. The façade emitter
emits the real `I<Module>Api` (`IKeyCustodianApi`, which exposes only `GetJwksAsync`
— byte-gated in the C# fixture parity table above); this 3-method signer-façade
(`SignAsync`/`SignDerivedAsync`/`AllScopesAsync`) exists only as the delegation
target the route-policy / gRPC / mutual-TLS harnesses fake implement. No emitter
call produces it, so it cannot get a re-emit byte-gate and is correctly outside the
sweep. It carries a normal copyright file header (NOT an `<auto-generated>` banner)
and a `.cs` (not `.g.cs`) extension, so its hand-authored status is honest; the
fixture façade deliberately keeps the `.Generated.Facade` namespace the real façade
emitter targets so harness consumers reference one façade namespace regardless of
which façade they fake.

---

## Coverage summary

| Metric                            | Result                                                                                                                                                                                                                                                                                                                                           |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Lines                             | 100%                                                                                                                                                                                                                                                                                                                                             |
| Branches                          | 100%                                                                                                                                                                                                                                                                                                                                             |
| Functions                         | 100%                                                                                                                                                                                                                                                                                                                                             |
| Statements                        | 100%                                                                                                                                                                                                                                                                                                                                             |
| Test files                        | 46 (3 new: `wire-channel.test.ts`, `wire-version-emitter.test.ts`, `wire-manifest-emitter.test.ts`)                                                                                                                                                                                                                                              |
| Total tests                       | ~1040 (vitest runner — 46 files; breakdown: wire-channel unit tests ~12, wire-version-emitter unit tests ~8, wire-manifest-emitter unit tests ~8, `proto-grpc-byte-parity.test.ts` additions ~2, `proto-grpc-emit.integration.test.ts` additions ~4) |
| C# behavior tests (D2.Edge.Tests) | 934 passing (includes the temporal round-trip matrix `TemporalRoundTripTests` + the enum-wire round-trip matrix `EnumWireRoundTripTests` + the `@d2Resilience` predicate retry matrix `PredicateRetryTests` + the predicate parity matrix `PredicateParityTests` — the flat `placeOrder` rows AND the nested/array-of-model `placeOrderV2` rows + the over-the-wire resilience suite `OverTheWireResilienceTests`) |
| TS temporal round-trip (@d2/time) | 32 (`temporal-round-trip.test.ts`, drives the shared cross-language fixture)                                                                                                                                                                                                                                                                     |
| TS enum-wire round-trip           | 7 (`enum-wire-round-trip.test.ts`, drives the shared `contracts/enum/enum-parity.fixture.json`)                                                                                                                                                                                                                                                  |
| TS predicate parity               | 6 (`predicate-parity.test.ts`, drives the shared flat `contracts/resilience/predicate-parity.fixture.json` AND the nested/array-of-model `contracts/resilience/predicate-parity-nested.fixture.json`)                                                                                                                                            |
| TS wire-channel unit tests        | ~12 (`wire-channel.test.ts`: WIRE_CHANNEL_GRAMMAR matches/rejects, `parseChannel` positive 3 cases, adversarial 8 inputs, `expectedCsharpChannelSegment` 4 round-trips, NON-VACUOUS D2TSP010 mismatch, positive agreement 3 cases, `@versioned` axis, adversarial namespace shape) |
| TS wire-version-emitter unit tests | ~8 (`wire-version-emitter.test.ts`: CHANNEL/GENERATION/STABILITY for alpha, stable STABILITY, namespace declaration, banner, `#nullable enable`, fileName) |
| TS wire-manifest-emitter unit tests | ~8 (`wire-manifest-emitter.test.ts`: 4 identity facts, `x-d2-generated-by`, valid JSON round-trip, NO package name keys 4 asserts, beta channel variant, fileName) |

---

## Over-the-wire resilience + envelope integration (real Kestrel socket — `OverTheWireResilienceTests`)

The closest-to-prod validation of the generated gRPC client + service + `D2ResultProto`
envelope + `D2.Shared.Resilience` pipeline end-to-end: a real Kestrel HTTPS endpoint on
`127.0.0.1:0` (real TCP socket + real TLS 1.3 handshake + real HTTP/2 + real protobuf
serialization + real `RpcException` propagation), server-TLS only (loopback self-signed
server cert, client-trust callback — NO client cert; resilience is auth-orthogonal, so the
mTLS client-cert requirement is dropped → runs cross-platform incl. Windows). The
real-socket host/channel plumbing (start a Kestrel HTTPS host on the ephemeral loopback
port, map the gRPC service, resolve the bound endpoint, dial it, and the
`RunningServer : IAsyncDisposable` handle) lives in the shared `GrpcTestHost` test-infra
helper (`D2.Edge.Tests` `TypeSpecGrpc/GrpcTestHost.cs`), re-used by both this suite and the
mutual-TLS harness; each supplies only its own service registration + service map + (for
mTLS) the client-cert SSL hook. Self-managed (ephemeral port; no `dotnet run`, single
process). The SAME generated `SignFixtureGrpcClient` / `PredicateFixturesGrpcClient` the
in-memory harness drives, re-proven over a real socket.

| Scenario | Fault injected (server shim) | Assertion |
| --- | --- | --- |
| Transient-recovery | `RpcException(Unavailable)` ×1 then success | `Success`, `CallCount == 2` (retry recovered over the wire) |
| Breaker open → half-open | `Unavailable` until threshold → open (fast-fail) → fake-clock past cooldown → probe success | open-window call does NOT reach the server (call-count frozen, 503); after cooldown the probe closes the breaker (`Success`); breaker clock injected (`CircuitBreakerOptions.NowFunc`) — deterministic, no wall-clock |
| No-amplification (captured envelope) | business `ValidationFailed` on the `D2ResultProto` envelope at gRPC status OK | `StatusCode == 400` (NOT 503/500), **`CallCount == 1`** (a returned business failure is a VALUE, never retried) — even with a 5-attempt retry pipeline |
| Envelope byte-fidelity | success+data / `Conflict`+errorCode / `NotFound` | reconstructed `D2Result` preserves status + category + error-code + data across real protobuf-over-HTTP/2 |
| `@d2Resilience` predicate | success `partial==true` (retryWhen) / `VALIDATION_FAILED` (failWhen) | retryWhen → `CallCount > 1` (sentinel opts the business result into retry); failWhen → `CallCount == 1`, returned verbatim |

**Determinism**: breaker window = injected `NowFunc` (test-mutable counter via single-element array
to keep the lambda capture stable); retry backoff = `BaseDelayMs:1 + Jitter:false + DelayFunc:
Task.CompletedTask`. No elapsed-time assertions; all timing is a controlled input. **Cross-platform**:
no client cert presented ⇒ the Windows-Schannel client-cert-context limitation (which gates the mTLS
harness's cert-presenting cases to non-Windows) does not apply — all five scenarios run on every
platform.

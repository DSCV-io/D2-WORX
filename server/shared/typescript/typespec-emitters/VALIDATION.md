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
| `GetJwksInput.g.cs` | `emitCsharpDtos("getJwks", "D2.Edge.Tests.TypeSpecDto.Generated", "contracts/typespec/key-custodian/key-custodian.tsp", [], [], [])` | Byte-identical to `GET_JWKS_INPUT_FIXTURE` | `tests/byte-parity.test.ts` | `byteParity_GetJwksInput_CommittedFixtureIdentical` |
| `GetJwksOutput.g.cs` | `emitCsharpDtos("getJwks", ..., [], outputFields, [nested("Jwk", ...)])` | Byte-identical to `GET_JWKS_OUTPUT_FIXTURE` | `tests/byte-parity.test.ts` | `byteParity_GetJwksOutput_CommittedFixtureIdentical` |
| `SignInput.g.cs` | `emitCsharpDtos("sign", ..., inputFields, [], [])` | Byte-identical to `SIGN_INPUT_FIXTURE` | `tests/byte-parity.test.ts` | `byteParity_SignInput_CommittedFixtureIdentical` |

**Deliberate-drift non-vacuity guard**: each byte-parity describe block contains a second
test that mutates the respective fixture by one byte and asserts the regenerated content
does NOT match (`not.toBe(driftedFixture)`). This proves the byte gate is not a tautology
comparing a buffer to itself — the guard covers `GetJwksInput`, `GetJwksOutput`, and
`SignInput`.

---

## C# structural validation table

| Live type | Generated type | Validated by |
|---|---|---|
| `D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks.GetJwksInput` | `D2.Edge.Tests.TypeSpecDto.Generated.GetJwksInput` | `TypeSpecDtoValidationTests.GeneratedGetJwksInput_IsParameterless_MatchingLiveDto` |
| `D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks.GetJwksOutput` | `D2.Edge.Tests.TypeSpecDto.Generated.GetJwksOutput` | `TypeSpecDtoValidationTests.GeneratedGetJwksOutput_HasSamePublicShape_AsLiveDto` |
| `D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk` (6 public members) | `D2.Edge.Tests.TypeSpecDto.Generated.Jwk` (6-field positional record) | `TypeSpecDtoValidationTests.GeneratedJwk_HasSameSixPublicMembers_AsLiveJwkVo` |
| `[property: RedactData]` on `SignInput.Payload` | `[property: RedactData(Reason = RedactReason.PersonalInformation)]` on generated `SignInput.Payload` | `TypeSpecDtoValidationTests.GeneratedSignInput_PayloadProperty_IsRedactedByRealPolicy` (real Serilog pipeline) |

**Redaction proof**: `GeneratedSignInput_PayloadProperty_IsRedactedByRealPolicy` builds a
real `LoggerConfiguration().Destructure.With<RedactDataDestructuringPolicy>()` pipeline (not a
mock), logs the generated `SignInput` record, and asserts the rendered output contains
`"[REDACTED: PersonalInformation]"` and does NOT contain `"SECRET_PAYLOAD"`.

**Transport-vs-domain-VO divergence note**: the live `Jwk` VO has 3 positional ctor params + 3
init-only properties with constant defaults (`Kty="RSA"`, `Use="sig"`, `Alg="RS256"`). The
generated `Jwk` DTO is a 6-field positional record. Equivalence is validated by public-member-set
comparison (6 public members, same names and types), NOT by constructor arity.

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

## Coverage summary

| Metric | Result |
|---|---|
| Lines | 100% |
| Branches | 100% |
| Functions | 100% |
| Statements | 100% |
| Test files | 12 |
| Total tests | 129 |

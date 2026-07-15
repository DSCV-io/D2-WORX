// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { D2ResultProto } from "@dcsv-io/d2-protos";
import { d2ResultFromProto } from "@dcsv-io/d2-grpc-client";
import { loadFixture } from "../src/index.js";

// ---------------------------------------------------------------------------
// Cross-runtime binary-fixture parity guard — .NET → node gRPC codec.
//
// THE TEST PROVES:
//   1. A D2Result serialized by the .NET codec (result.ToProto().ToByteArray())
//      re-materializes via the node codec (D2ResultProto.decode → d2ResultFromProto)
//      into an EQUAL D2Result — the real consumer path (node BFF calling a .NET
//      service over gRPC).
//
//   2. EVERY carried field survives the boundary: success, statusCode (exact
//      integer — no lossy gRPC-bucket remapping), errorCode, category (snake
//      wire string), traceId, messages (key + every param), inputErrors (field
//      + errors key + every param).
//
//   3. The test is NON-VACUOUS: a wrong byte in the proto (e.g. a category byte
//      flipped) would cause D2ResultProto.decode to decode a different value,
//      which d2ResultFromProto maps to a different category, which fails the
//      strict toBe assertion. A planted wrong-category base64 in a case would
//      RED that case.
//
// SHAPES COVERED (13 cases):
//   ok, ok-with-trace-id, not-found, conflict, validation-failed (multi-field
//   with params), unauthorized, service-unavailable, some-found, too-many-
//   requests, payload-too-large, unhandled-exception, canceled,
//   raw-fail-no-category.
//   Together: every ErrorCategory value + the category-absent path.
//
// DATA SOURCE:
//   fixtures/grpc-result-codec/cases.json — emitted by GrpcResultCodecFixtureEmitter
//   (.NET, DcsvIo.D2.Tests). The protoBase64 field is the actual .NET codec
//   output; the expected field is the originating D2Result's field values.
// ---------------------------------------------------------------------------

interface TKMessageExpected {
  readonly key: string;
  readonly params?: Readonly<Record<string, string>>;
}

interface InputErrorExpected {
  readonly field: string;
  readonly errors: readonly TKMessageExpected[];
}

/**
 * Wire-boundary shape for a single fixture case's expected values.
 *
 * `errorCode`, `category`, and `traceId` are typed `string | null` — not
 * `string | undefined` — because these fields mirror the literal shape of
 * `cases.json` as emitted by the .NET fixture builder. The .NET fixture writes
 * JSON `null` (not the key's absence) as the sentinel for "this field was not
 * set on the originating D2Result". Deserializing that JSON into a TypeScript
 * interface produces `null` for absent fields. The assertions in this file
 * unify node `undefined` to `null` via `?? null` so that both sides compare
 * against the same sentinel. This `string | null` typing is intentionally
 * scoped to this fixture-wire-boundary interface; domain types elsewhere in
 * the codebase use `string | undefined`.
 */
interface CaseExpected {
  readonly success: boolean;
  readonly statusCode: number;
  readonly errorCode: string | null;
  readonly category: string | null;
  readonly traceId: string | null;
  readonly messages: readonly TKMessageExpected[];
  readonly inputErrors: readonly InputErrorExpected[];
}

interface Case {
  readonly name: string;
  readonly protoBase64: string;
  readonly expected: CaseExpected;
}

/** Decode a base64 string to a Uint8Array (works in Node.js ESM). */
function base64ToBytes(b64: string): Uint8Array {
  return Buffer.from(b64, "base64");
}

describe("grpc-result-codec parity (.NET codec → node codec, binary fixture)", () => {
  const fixture = loadFixture<Case[]>("grpc-result-codec", "cases");
  const cases = fixture.data;

  // Sanity: the fixture must be non-empty (mis-path would give [])
  it("fixture has cases", () => {
    expect(cases.length).toBeGreaterThan(0);
  });

  // Per-case parity: decode the .NET-serialized proto bytes, run through the
  // node codec, assert every field equals the .NET-captured expected.
  for (const c of cases) {
    describe(`case: ${c.name}`, () => {
      const bytes = base64ToBytes(c.protoBase64);
      const proto = D2ResultProto.decode(bytes);
      const result = d2ResultFromProto(proto);
      const exp = c.expected;

      it("success matches", () => {
        expect(result.success).toBe(exp.success);
      });

      it("statusCode matches (exact integer — no lossy gRPC-bucket remapping)", () => {
        expect(result.statusCode).toBe(exp.statusCode);
      });

      it("errorCode matches", () => {
        // .NET null → node undefined; both represent "absent"
        const got = result.errorCode ?? null;
        expect(got).toBe(exp.errorCode);
      });

      it("category matches (snake wire string)", () => {
        const got = result.category ?? null;
        expect(got).toBe(exp.category);
      });

      it("traceId matches", () => {
        const got = result.traceId ?? null;
        expect(got).toBe(exp.traceId);
      });

      it("messages length matches", () => {
        expect(result.messages.length).toBe(exp.messages.length);
      });

      for (let i = 0; i < exp.messages.length; i++) {
        const expMsg = exp.messages[i]!;
        it(`messages[${i}].key matches`, () => {
          expect(result.messages[i]?.key).toBe(expMsg.key);
        });

        if (expMsg.params && Object.keys(expMsg.params).length > 0) {
          it(`messages[${i}].params key count matches`, () => {
            expect(Object.keys(result.messages[i]?.params ?? {}).length).toBe(
              Object.keys(expMsg.params ?? {}).length,
            );
          });
          for (const [k, v] of Object.entries(expMsg.params)) {
            it(`messages[${i}].params.${k} matches`, () => {
              const gotParams = result.messages[i]?.params as
                | Record<string, unknown>
                | undefined;
              expect(gotParams?.[k]).toBe(v);
            });
          }
        } else {
          it(`messages[${i}].params is absent (no params)`, () => {
            const gotParams = result.messages[i]?.params;
            expect(
              gotParams === undefined || Object.keys(gotParams).length === 0,
            ).toBe(true);
          });
        }
      }

      it("inputErrors length matches", () => {
        expect(result.inputErrors.length).toBe(exp.inputErrors.length);
      });

      for (let i = 0; i < exp.inputErrors.length; i++) {
        const expIe = exp.inputErrors[i]!;
        it(`inputErrors[${i}].field matches`, () => {
          expect(result.inputErrors[i]?.field).toBe(expIe.field);
        });

        it(`inputErrors[${i}].errors length matches`, () => {
          expect(result.inputErrors[i]?.errors.length).toBe(
            expIe.errors.length,
          );
        });

        for (let j = 0; j < expIe.errors.length; j++) {
          const expErr = expIe.errors[j]!;
          it(`inputErrors[${i}].errors[${j}].key matches`, () => {
            expect(result.inputErrors[i]?.errors[j]?.key).toBe(expErr.key);
          });

          if (expErr.params && Object.keys(expErr.params).length > 0) {
            it(`inputErrors[${i}].errors[${j}].params key count matches`, () => {
              expect(
                Object.keys(result.inputErrors[i]?.errors[j]?.params ?? {})
                  .length,
              ).toBe(Object.keys(expErr.params ?? {}).length);
            });
            for (const [k, v] of Object.entries(expErr.params)) {
              it(`inputErrors[${i}].errors[${j}].params.${k} matches`, () => {
                const gotParams = result.inputErrors[i]?.errors[j]?.params as
                  | Record<string, unknown>
                  | undefined;
                expect(gotParams?.[k]).toBe(v);
              });
            }
          } else {
            it(`inputErrors[${i}].errors[${j}].params is absent`, () => {
              const gotParams = result.inputErrors[i]?.errors[j]?.params;
              expect(
                gotParams === undefined || Object.keys(gotParams).length === 0,
              ).toBe(true);
            });
          }
        }
      }
    });
  }

  // ---------------------------------------------------------------------------
  // Non-vacuous guard: a corrupt proto byte must cause at least one field to
  // mismatch. We verify the test WOULD red by using a known-bad base64 string
  // (all-zero bytes) — the zero-byte proto decodes to success=false,
  // statusCode=0, which differs from case "ok" (success=true, statusCode=200).
  // This is a reasoning proof, not a planted fixture; no bad fixture is left
  // behind.
  // ---------------------------------------------------------------------------
  it("non-vacuous: zero-byte proto decodes to different values than case ok", () => {
    const zeroBytesProto = D2ResultProto.decode(new Uint8Array(0));
    const zeroResult = d2ResultFromProto(zeroBytesProto);
    // The ok case expects success=true, statusCode=200.
    // A zero-byte proto gives success=false (default bool), statusCode=0.
    expect(zeroResult.success).toBe(false);
    expect(zeroResult.statusCode).toBe(0);
    // Confirm these differ from the ok case expected values.
    const okCase = cases.find((c) => c.name === "ok");
    expect(okCase).toBeDefined();
    expect(zeroResult.success).not.toBe(okCase!.expected.success);
    expect(zeroResult.statusCode).not.toBe(okCase!.expected.statusCode);
  });
});

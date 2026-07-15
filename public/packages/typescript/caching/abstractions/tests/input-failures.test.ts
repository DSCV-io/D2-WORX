// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { TK } from "@dcsv-io/d2-i18n-keys";
import { ErrorCodes, HttpStatusCode } from "@dcsv-io/d2-result";
import { describe, expect, it } from "vitest";

import { InputFailures } from "../src/index.js";

/**
 * Pins the wire-shape of `InputFailures.required` / `required<T>`.
 * Twin of .NET `Unit/Caching/Abstractions/InputFailuresTests`.
 * Cache-impl call sites depend on the exact shape (`success === false`,
 * `ErrorCodes.VALIDATION_FAILED`, status 400, single `inputError`
 * carrying the param name + `TK.common.errors.NOT_NULL_VIOLATION`).
 */

describe("InputFailures.required", () => {
  it("required_emptyParamName_stillBuildsValidationFailed", () => {
    const result = InputFailures.required("");

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe("");
  });

  it("required_whitespaceParamName_buildsValidationFailedWithThatField", () => {
    const result = InputFailures.required("   ");

    expect(result.success).toBe(false);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe("   ");
  });

  it.each([
    "key",
    "keys",
    "entries",
    "paramName",
    "anyArbitraryParameterName",
  ] as const)(
    "required_paramName_propagatesVerbatim_theory (%s)",
    (paramName) => {
      const generic = InputFailures.required<number>(paramName);
      const nonGeneric = InputFailures.required(paramName);

      expect(generic.inputErrors[0]?.field).toBe(paramName);
      expect(nonGeneric.inputErrors[0]?.field).toBe(paramName);
    },
  );

  it("required_wireShape_successFalse_validationFailedCode_status400_singleInputError_fieldEqualsParam_tkNotNullViolation", () => {
    const paramName = "key";
    const result = InputFailures.required<string>(paramName);

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.statusCode).toBe(400);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe(paramName);
    expect(result.inputErrors[0]?.errors).toEqual([
      TK.common.errors.NOT_NULL_VIOLATION,
    ]);
  });

  it("required_nonGeneric_matchesGeneric_errorShape", () => {
    const paramName = "lockId";
    const generic = InputFailures.required<boolean>(paramName);
    const nonGeneric = InputFailures.required(paramName);

    expect(generic.success).toBe(nonGeneric.success);
    expect(generic.errorCode).toBe(nonGeneric.errorCode);
    expect(generic.statusCode).toBe(nonGeneric.statusCode);
    expect(generic.inputErrors).toEqual(nonGeneric.inputErrors);
  });

  it("required_generic_dataIsUndefined", () => {
    const result = InputFailures.required<string>("key");

    expect(result.data).toBeUndefined();
  });

  it("required_paramNameWithControlChars_isPreservedAsFieldNameWithoutThrow", () => {
    const paramName = "key\nwith\tcontrol\0chars";
    const result = InputFailures.required(paramName);

    expect(result.success).toBe(false);
    expect(result.inputErrors[0]?.field).toBe(paramName);
  });
});

describe("InputFailures.invalid", () => {
  it("invalid_wireShape_usesValidationFailedTkNotNotNull", () => {
    const paramName = "amount";
    const result = InputFailures.invalid<number>(paramName);

    expect(result.success).toBe(false);
    expect(result.errorCode).toBe(ErrorCodes.VALIDATION_FAILED);
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toHaveLength(1);
    expect(result.inputErrors[0]?.field).toBe(paramName);
    expect(result.inputErrors[0]?.errors).toEqual([
      TK.common.errors.VALIDATION_FAILED,
    ]);
  });

  it("invalid_nonGeneric_matchesGeneric_errorShape", () => {
    const paramName = "expirationMs";
    const generic = InputFailures.invalid<boolean>(paramName);
    const nonGeneric = InputFailures.invalid(paramName);

    expect(generic.success).toBe(nonGeneric.success);
    expect(generic.errorCode).toBe(nonGeneric.errorCode);
    expect(generic.statusCode).toBe(nonGeneric.statusCode);
    expect(generic.inputErrors).toEqual(nonGeneric.inputErrors);
  });
});

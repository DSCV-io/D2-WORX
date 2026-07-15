// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  diagError,
  diagWarning,
  DiagnosticIds,
  formatDiagnostic,
} from "../src/lib/diagnostics.js";

describe("diagError / diagWarning", () => {
  it("error severity", () => {
    expect(diagError("D2X", "boom")).toEqual({
      id: "D2X",
      severity: "error",
      message: "boom",
    });
  });
  it("warning severity", () => {
    expect(diagWarning("D2X", "soft")).toEqual({
      id: "D2X",
      severity: "warning",
      message: "soft",
    });
  });
  it("optional filePath surfaces", () => {
    expect(diagError("D2X", "boom", "x.json")).toEqual({
      id: "D2X",
      severity: "error",
      message: "boom",
      filePath: "x.json",
    });
    expect(diagWarning("D2X", "soft", "x.json")).toEqual({
      id: "D2X",
      severity: "warning",
      message: "soft",
      filePath: "x.json",
    });
  });
});

describe("formatDiagnostic", () => {
  it("renders without filePath", () => {
    expect(formatDiagnostic(diagError("D2X", "boom"))).toBe("ERROR D2X: boom");
  });
  it("renders with filePath", () => {
    expect(formatDiagnostic(diagWarning("D2X", "soft", "y.json"))).toBe(
      "WARNING D2X y.json: soft",
    );
  });
});

describe("DiagnosticIds", () => {
  it.each([
    ["CTX_DUPLICATE_PROPERTY", "D2CTX001"],
    ["CTX_INVALID_TYPE", "D2CTX002"],
    ["CTX_INVALID_NAMESPACE", "D2CTX003"],
    ["CTX_INVALID_NAME", "D2CTX004"],
    ["CTX_EXTENDS_UNRESOLVED", "D2CTX005"],
    ["CTX_MALFORMED_SPEC", "D2CTX006"],
    ["SCP_DUPLICATE", "D2SCP001"],
    ["SCP_INVALID_NAME", "D2SCP002"],
    ["SCP_INVALID_SENSITIVITY", "D2SCP003"],
    ["SCP_MALFORMED_SPEC", "D2SCP009"],
    ["AEC_DUPLICATE_CODE", "D2AEC001"],
    ["AEC_DUPLICATE_FACTORY", "D2AEC002"],
    ["AEC_UNKNOWN_CATEGORY", "D2AEC003"],
    ["AEC_INVALID_HTTP_STATUS", "D2AEC004"],
    ["AEC_MALFORMED_SPEC", "D2AEC005"],
    ["FC_MALFORMED_SPEC", "D2FC001"],
    ["FC_DUPLICATE_CONST_NAME", "D2FC002"],
    ["FC_INVALID_CONST_NAME", "D2FC003"],
    ["FC_NON_POSITIVE_VALUE", "D2FC004"],
    ["FC_DUPLICATE_ENUM_NAME", "D2FC005"],
    ["FC_INVALID_ENUM_NAME", "D2FC006"],
    ["FC_EMPTY_ENUM_MEMBER_LIST", "D2FC007"],
    ["FC_DUPLICATE_ENUM_MEMBER", "D2FC008"],
    ["FC_INVALID_ENUM_MEMBER_NAME", "D2FC009"],
  ])("DiagnosticIds.%s = %s", (key, value) => {
    expect(DiagnosticIds[key as keyof typeof DiagnosticIds]).toBe(value);
  });
});

// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Unit tests for the wire-channel derivation and cross-validation module.
//
// Covers:
//   parseChannel — positive cases (v2alpha, v2beta, stable v2) + adversarial
//   expectedCsharpChannelSegment — round-trip checks
//   validateChannelAgreement — positive agreement, NON-VACUOUS mismatch (D2TSP010),
//     @versioned-axis mismatch, adversarial namespace shapes

import { describe, it, expect, vi } from "vitest";
import {
  WIRE_CHANNEL_GRAMMAR,
  parseChannel,
  expectedCsharpChannelSegment,
  validateChannelAgreement,
} from "../src/lib/wire-channel.js";

// ---------------------------------------------------------------------------
// WIRE_CHANNEL_GRAMMAR
// ---------------------------------------------------------------------------

describe("wireChannel_Grammar_MatchesAndRejects", () => {
  it("matches a valid alpha channel", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.v2alpha")).toBe(true);
  });

  it("matches a valid beta channel", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.geo.v3beta")).toBe(true);
  });

  it("matches a stable (no-suffix) channel", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.auth.v1")).toBe(true);
  });

  it("rejects uppercase service prefix", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("D2.sample.v2alpha")).toBe(false);
  });

  it("rejects missing version number after v", () => {
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.sample.valpha")).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// parseChannel — positive cases
// ---------------------------------------------------------------------------

describe("parseChannel_AlphaChannel_ReturnsFullTriple", () => {
  it("parses d2.sample.v2alpha into the full WireChannel triple", () => {
    const result = parseChannel("d2.sample.v2alpha");
    expect(result).toBeDefined();
    expect(result!.svc).toBe("sample");
    expect(result!.generation).toBe(2);
    expect(result!.stability).toBe("alpha");
    expect(result!.lowerChannel).toBe("v2alpha");
    expect(result!.pascalChannel).toBe("V2Alpha");
  });
});

describe("parseChannel_BetaChannel_ReturnsBetaStability", () => {
  it("parses v2beta → stability 'beta' and pascalChannel 'V2Beta'", () => {
    const result = parseChannel("d2.geo.v2beta");
    expect(result).toBeDefined();
    expect(result!.stability).toBe("beta");
    expect(result!.pascalChannel).toBe("V2Beta");
    expect(result!.lowerChannel).toBe("v2beta");
  });
});

describe("parseChannel_StableChannel_ReturnsStableStability", () => {
  it("parses v2 (no suffix) → stability 'stable' and pascalChannel 'V2'", () => {
    const result = parseChannel("d2.auth.v2");
    expect(result).toBeDefined();
    expect(result!.stability).toBe("stable");
    expect(result!.lowerChannel).toBe("v2");
    expect(result!.pascalChannel).toBe("V2");
  });
});

// ---------------------------------------------------------------------------
// parseChannel — adversarial (mirrors proto-emitter.test.ts:1528-1536)
// ---------------------------------------------------------------------------

describe("parseChannel_Adversarial_ReturnsUndefined", () => {
  it("returns undefined for uppercase proto-package prefix", () => {
    expect(parseChannel("D2.sample.v2alpha")).toBeUndefined();
  });

  it("returns undefined for missing v before the number", () => {
    expect(parseChannel("d2.sample.2alpha")).toBeUndefined();
  });

  it("returns undefined for an extra dot in the channel segment", () => {
    expect(parseChannel("d2.sample.v2.alpha")).toBeUndefined();
  });

  it("returns undefined for an unsupported stability suffix", () => {
    expect(parseChannel("d2.sample.v2gamma")).toBeUndefined();
  });

  it("returns undefined for missing version number after v (valpha)", () => {
    expect(parseChannel("d2.sample.valpha")).toBeUndefined();
  });

  it("returns undefined for empty string", () => {
    expect(parseChannel("")).toBeUndefined();
  });

  it("returns undefined for a random garbage string", () => {
    expect(parseChannel("garbage")).toBeUndefined();
  });

  it("returns undefined when the channel segment is absent", () => {
    expect(parseChannel("d2.sample")).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// expectedCsharpChannelSegment — round-trips
// ---------------------------------------------------------------------------

describe("expectedCsharpChannelSegment_RoundTrips", () => {
  it("converts v2alpha to V2Alpha", () => {
    expect(expectedCsharpChannelSegment("v2alpha")).toBe("V2Alpha");
  });

  it("converts v2beta to V2Beta", () => {
    expect(expectedCsharpChannelSegment("v2beta")).toBe("V2Beta");
  });

  it("converts v2 (stable) to V2", () => {
    expect(expectedCsharpChannelSegment("v2")).toBe("V2");
  });

  it("converts v3alpha to V3Alpha", () => {
    expect(expectedCsharpChannelSegment("v3alpha")).toBe("V3Alpha");
  });
});

// ---------------------------------------------------------------------------
// validateChannelAgreement — positive agreement
// ---------------------------------------------------------------------------

describe("validateChannelAgreement_Agreement_PassesAndReturnsChannel", () => {
  it("agreeing proto-package and proto-csharp-namespace → no error, returns WireChannel", () => {
    const onError = vi.fn();
    const result = validateChannelAgreement(
      "d2.sample.v2alpha",
      "D2.Services.Protos.Sample.V2Alpha",
      undefined,
      onError,
    );
    expect(onError).not.toHaveBeenCalled();
    expect(result).toBeDefined();
    expect(result!.lowerChannel).toBe("v2alpha");
  });

  it("agreeing proto-package and @versioned channel → no error", () => {
    const onError = vi.fn();
    const result = validateChannelAgreement(
      "d2.sample.v2alpha",
      "D2.Services.Protos.Sample.V2Alpha",
      "v2alpha",
      onError,
    );
    expect(onError).not.toHaveBeenCalled();
    expect(result).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// validateChannelAgreement — NON-VACUOUS D2TSP010 mismatch
// Proves the diagnostic ACTUALLY fires on a real mismatch (§1.20).
// ---------------------------------------------------------------------------

describe("validateChannelAgreement_CsharpNsMismatch_FiresD2TSP010", () => {
  it("proto-package v2alpha vs proto-csharp-namespace V2Beta → onError called once with channel-segment-mismatch", () => {
    const onError = vi.fn();
    const result = validateChannelAgreement(
      "d2.sample.v2alpha",
      "D2.Services.Protos.Sample.V2Beta",
      undefined,
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("channel-segment-mismatch");
    const msg = onError.mock.calls[0]![1] as string;
    expect(msg).toContain("D2TSP010");
    expect(msg).toContain("v2alpha");
    expect(msg).toContain("V2Beta");
  });
});

describe("validateChannelAgreement_VersionedChannelMismatch_FiresD2TSP010", () => {
  it("@versioned channel v2beta vs proto-package v2alpha → onError called with channel-segment-mismatch", () => {
    const onError = vi.fn();
    const result = validateChannelAgreement(
      "d2.sample.v2alpha",
      "D2.Services.Protos.Sample.V2Alpha",
      "v2beta",
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("channel-segment-mismatch");
    const msg = onError.mock.calls[0]![1] as string;
    expect(msg).toContain("D2TSP010");
    expect(msg).toContain("v2beta");
    expect(msg).toContain("v2alpha");
  });
});

// ---------------------------------------------------------------------------
// validateChannelAgreement — adversarial namespace shapes
// ---------------------------------------------------------------------------

describe("validateChannelAgreement_NoChannelInNamespace_MismatchFires", () => {
  it("proto-csharp-namespace with no channel trailing segment → mismatch fires", () => {
    const onError = vi.fn();
    const result = validateChannelAgreement(
      "d2.sample.v2alpha",
      "D2.Foo.Bar",
      undefined,
      onError,
    );
    expect(result).toBeUndefined();
    expect(onError).toHaveBeenCalledOnce();
    expect(onError.mock.calls[0]![0]).toBe("channel-segment-mismatch");
  });
});

describe("validateChannelAgreement_UnparseableProtoPackage_ReturnsUndefined", () => {
  it("unparseable proto-package → returns undefined without calling onError", () => {
    const onError = vi.fn();
    const result = validateChannelAgreement(
      "not-a-valid-proto-package",
      "D2.Foo.V2Alpha",
      undefined,
      onError,
    );
    expect(result).toBeUndefined();
    // Grammar failure is a separate concern; channel-mismatch check is skipped.
    expect(onError).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Adversarial grammar: mixed-case, multi-digit, non-matching passthrough
// ---------------------------------------------------------------------------

describe("wireChannel_Grammar_RejectsMixedCaseStabilitySuffix", () => {
  it("mixed-case stability suffix is rejected by grammar and parseChannel returns undefined", () => {
    // Grammar requires lowercase: (alpha|beta)? — uppercase 'A' in 'v2Alpha' does not match.
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.auth.v2Alpha")).toBe(false);
    expect(parseChannel("d2.auth.v2Alpha")).toBeUndefined();
  });
});

describe("parseChannel_MultiDigitVersion_AcceptedAndParsedCorrectly", () => {
  it("v10alpha parses into generation 10, stability alpha, and correct channel strings", () => {
    // Grammar v\d+ matches multiple digits — v10 is valid.
    expect(WIRE_CHANNEL_GRAMMAR.test("d2.auth.v10alpha")).toBe(true);
    const result = parseChannel("d2.auth.v10alpha");
    expect(result).toBeDefined();
    expect(result!.generation).toBe(10);
    expect(result!.stability).toBe("alpha");
    expect(result!.lowerChannel).toBe("v10alpha");
    expect(result!.pascalChannel).toBe("V10Alpha");
  });
});

describe("expectedCsharpChannelSegment_NonMatchingInput_ReturnedUnchanged", () => {
  it("non-matching input is returned unchanged (String.replace passthrough when regex has no match)", () => {
    // The internal regex /^(v)(\d+)(alpha|beta)?$/ does not match "garbage",
    // so String.replace leaves the string untouched.
    expect(expectedCsharpChannelSegment("garbage")).toBe("garbage");
  });
});

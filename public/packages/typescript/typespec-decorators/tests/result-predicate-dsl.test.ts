// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Pure-unit tests for the @d2Resilience result-predicate DSL parser
// (src/result-predicate-dsl.ts). These tests run the parser directly without a
// TypeSpec compile host.
// Valid expressions: assert exact AST structure (envelope fields, data paths,
// array accessors, boolean precedence, grouping, in-lists).
// Invalid expressions: assert each error code is present AND ok === false
// (non-vacuous — a warning-severity slip or a parse that secretly succeeds
// would be caught).

import { describe, it, expect } from "vitest";
import { parseResultPredicate } from "../src/result-predicate-dsl.js";
import type {
  ArrayAccessorSegment,
  ComparisonNode,
  DataPathNode,
  PredicateNode,
} from "../src/result-predicate-dsl.js";

// ----------------------------------------------------------------
// Helpers
// ----------------------------------------------------------------

function assertOk(
  result: ReturnType<typeof parseResultPredicate>,
): PredicateNode {
  expect(result.ok).toBe(true);
  if (!result.ok) throw new Error("Expected ok");
  return result.root;
}

function assertFail(
  result: ReturnType<typeof parseResultPredicate>,
  expectedCode: string,
): void {
  // Non-vacuous: the parse MUST have failed, not merely surfaced a code.
  expect(result.ok).toBe(false);
  if (result.ok) throw new Error("Expected failure");
  const codes = result.errors.map((e) => e.code);
  expect(codes).toContain(expectedCode);
}

function asComparison(node: PredicateNode): ComparisonNode {
  expect(node.kind).toBe("comparison");
  if (node.kind !== "comparison") throw new Error("Expected comparison");
  return node;
}

function asDataPath(node: ComparisonNode): DataPathNode {
  expect(node.access.kind).toBe("dataPath");
  if (node.access.kind !== "dataPath") throw new Error("Expected dataPath");
  return node.access;
}

function asBooleanAccessPath(node: PredicateNode): DataPathNode {
  expect(node.kind).toBe("booleanAccess");
  if (node.kind !== "booleanAccess") throw new Error("Expected booleanAccess");
  return node.access;
}

// ----------------------------------------------------------------
// Valid — envelope fields
// ----------------------------------------------------------------

describe("parse_EnvelopeSuccessBool_Ok", () => {
  it("parses result.success == false to an envelope comparison", () => {
    const root = asComparison(
      assertOk(parseResultPredicate("result.success == false")),
    );
    expect(root.access).toEqual({ kind: "envelope", field: "success" });
    expect(root.op).toBe("==");
    expect(root.rhs).toEqual({ kind: "bool", value: "false" });
  });
});

describe("parse_EnvelopeStatusCodeInt_Ok", () => {
  it("parses result.statusCode == 400", () => {
    const root = asComparison(
      assertOk(parseResultPredicate("result.statusCode == 400")),
    );
    expect(root.access).toEqual({ kind: "envelope", field: "statusCode" });
    expect(root.rhs).toEqual({ kind: "int", value: "400" });
  });
});

describe("parse_EnvelopeErrorCodeString_Ok", () => {
  it('parses result.errorCode == "VALIDATION_FAILED"', () => {
    const root = asComparison(
      assertOk(parseResultPredicate('result.errorCode == "VALIDATION_FAILED"')),
    );
    expect(root.access).toEqual({ kind: "envelope", field: "errorCode" });
    expect(root.rhs).toEqual({ kind: "string", value: "VALIDATION_FAILED" });
  });
});

describe("parse_EnvelopeCategoryString_Ok", () => {
  it('parses result.category == "infrastructure_unavailable"', () => {
    const root = asComparison(
      assertOk(
        parseResultPredicate('result.category == "infrastructure_unavailable"'),
      ),
    );
    expect(root.access).toEqual({ kind: "envelope", field: "category" });
  });
});

describe("parse_EnvelopeNotEqual_Ok", () => {
  it('parses result.errorCode != "NOT_FOUND" with the != operator', () => {
    const root = asComparison(
      assertOk(parseResultPredicate('result.errorCode != "NOT_FOUND"')),
    );
    expect(root.op).toBe("!=");
  });
});

// ----------------------------------------------------------------
// Valid — in() membership
// ----------------------------------------------------------------

describe("parse_ErrorCodeInList_Ok", () => {
  it('parses result.errorCode in ("SERVICE_UNAVAILABLE", "RATE_LIMITED")', () => {
    const root = asComparison(
      assertOk(
        parseResultPredicate(
          'result.errorCode in ("SERVICE_UNAVAILABLE", "RATE_LIMITED")',
        ),
      ),
    );
    expect(root.op).toBe("in");
    expect(root.rhs).toEqual([
      { kind: "string", value: "SERVICE_UNAVAILABLE" },
      { kind: "string", value: "RATE_LIMITED" },
    ]);
  });
});

describe("parse_StatusCodeInSingleValue_Ok", () => {
  it("parses result.statusCode in (503) — a single-element in-list", () => {
    const root = asComparison(
      parseResultPredicate("result.statusCode in (503)").ok
        ? assertOk(parseResultPredicate("result.statusCode in (503)"))
        : (() => {
            throw new Error("unreachable");
          })(),
    );
    expect(root.op).toBe("in");
    expect(root.rhs).toEqual([{ kind: "int", value: "503" }]);
  });
});

// ----------------------------------------------------------------
// Valid — boolean precedence + grouping
// ----------------------------------------------------------------

describe("parse_BooleanAnd_Ok", () => {
  it("parses result.success == false && result.data.partial == true", () => {
    const root = assertOk(
      parseResultPredicate(
        "result.success == false && result.data.partial == true",
      ),
    );
    expect(root.kind).toBe("bool");
    if (root.kind !== "bool") throw new Error("Expected bool");
    expect(root.op).toBe("&&");
    expect(root.left.kind).toBe("comparison");
    expect(root.right.kind).toBe("comparison");
  });
});

describe("parse_AndBindsTighterThanOr_Ok", () => {
  it("parses a || b && c as a || (b && c)", () => {
    const root = assertOk(
      parseResultPredicate(
        "result.success == true || result.statusCode == 1 && result.statusCode == 2",
      ),
    );
    // Top node is the OR; its right child is the AND.
    expect(root.kind).toBe("bool");
    if (root.kind !== "bool") throw new Error("Expected bool");
    expect(root.op).toBe("||");
    expect(root.right.kind).toBe("bool");
    if (root.right.kind !== "bool") throw new Error("Expected nested bool");
    expect(root.right.op).toBe("&&");
  });
});

describe("parse_ParenthesizedGrouping_Ok", () => {
  it("parses (a || b) && c with the OR grouped under the AND", () => {
    const root = assertOk(
      parseResultPredicate(
        "(result.success == true || result.statusCode == 1) && result.statusCode == 2",
      ),
    );
    expect(root.kind).toBe("bool");
    if (root.kind !== "bool") throw new Error("Expected bool");
    expect(root.op).toBe("&&");
    // Left is the parenthesized OR.
    expect(root.left.kind).toBe("bool");
    if (root.left.kind !== "bool") throw new Error("Expected grouped OR");
    expect(root.left.op).toBe("||");
  });
});

// ----------------------------------------------------------------
// Valid — data paths
// ----------------------------------------------------------------

describe("parse_FlatDataField_Ok", () => {
  it("parses result.data.partial == true to a single-segment data path", () => {
    const dp = asDataPath(
      asComparison(
        assertOk(parseResultPredicate("result.data.partial == true")),
      ),
    );
    expect(dp.root).toBe("data");
    expect(dp.segments).toEqual([{ kind: "field", name: "partial" }]);
  });
});

describe("parse_NestedDataPath_Ok", () => {
  it('parses result.data.order.customer.tier == "TRIAL" to a 3-segment path', () => {
    const dp = asDataPath(
      asComparison(
        assertOk(
          parseResultPredicate('result.data.order.customer.tier == "TRIAL"'),
        ),
      ),
    );
    expect(dp.segments).toEqual([
      { kind: "field", name: "order" },
      { kind: "field", name: "customer" },
      { kind: "field", name: "tier" },
    ]);
  });
});

describe("parse_ArrayCount_Ok", () => {
  it("parses result.data.items.count == 0 with a count accessor", () => {
    const dp = asDataPath(
      asComparison(
        assertOk(parseResultPredicate("result.data.items.count == 0")),
      ),
    );
    expect(dp.segments[0]).toEqual({ kind: "field", name: "items" });
    expect(dp.segments[1]).toEqual({
      kind: "arrayAccessor",
      accessor: "count",
    });
  });
});

describe("parse_ArrayAnyQuantifier_Ok", () => {
  it('parses result.data.items.any(i => i.status == "PENDING")', () => {
    const dp = asBooleanAccessPath(
      assertOk(
        parseResultPredicate(
          'result.data.items.any(i => i.status == "PENDING")',
        ),
      ),
    );
    const seg = dp.segments[1] as ArrayAccessorSegment;
    expect(seg.accessor).toBe("any");
    expect(seg.elemVar).toBe("i");
    expect(seg.subPredicate).toBeDefined();
    const sub = asComparison(seg.subPredicate!);
    expect(sub.access.kind).toBe("dataPath");
    if (sub.access.kind !== "dataPath") throw new Error("Expected dataPath");
    expect(sub.access.root).toBe("i");
    expect(sub.access.segments).toEqual([{ kind: "field", name: "status" }]);
    expect(sub.rhs).toEqual({ kind: "string", value: "PENDING" });
  });
});

describe("parse_ArrayAllNestedPathInSubPredicate_Ok", () => {
  it("parses result.data.batches.all(b => b.result.success == false)", () => {
    const dp = asBooleanAccessPath(
      assertOk(
        parseResultPredicate(
          "result.data.batches.all(b => b.result.success == false)",
        ),
      ),
    );
    const seg = dp.segments[1] as ArrayAccessorSegment;
    expect(seg.accessor).toBe("all");
    expect(seg.elemVar).toBe("b");
    const sub = asComparison(seg.subPredicate!);
    expect(sub.access.kind).toBe("dataPath");
    if (sub.access.kind !== "dataPath") throw new Error("Expected dataPath");
    // Inside the sub-predicate, "result" is a plain field of the element model.
    expect(sub.access.root).toBe("b");
    expect(sub.access.segments).toEqual([
      { kind: "field", name: "result" },
      { kind: "field", name: "success" },
    ]);
  });
});

describe("parse_ArrayContains_Ok", () => {
  it('parses result.data.retryCodes.contains("RETRY_ME")', () => {
    const dp = asBooleanAccessPath(
      assertOk(
        parseResultPredicate('result.data.retryCodes.contains("RETRY_ME")'),
      ),
    );
    const seg = dp.segments[1] as ArrayAccessorSegment;
    expect(seg.accessor).toBe("contains");
    expect(seg.literal).toEqual({ kind: "string", value: "RETRY_ME" });
  });
});

describe("parse_MixedEnvelopeAndQuantifier_Ok", () => {
  it("parses category OR a quantifier over items", () => {
    const root = assertOk(
      parseResultPredicate(
        'result.category == "infrastructure_unavailable" || result.data.items.any(i => i.status == "PENDING")',
      ),
    );
    expect(root.kind).toBe("bool");
    if (root.kind !== "bool") throw new Error("Expected bool");
    expect(root.op).toBe("||");
  });
});

describe("parse_NegativeIntLiteral_Ok", () => {
  it("parses a negative integer literal", () => {
    const root = asComparison(
      parseResultPredicate("result.statusCode == -1").ok
        ? assertOk(parseResultPredicate("result.statusCode == -1"))
        : (() => {
            throw new Error("unreachable");
          })(),
    );
    expect(root.rhs).toEqual({ kind: "int", value: "-1" });
  });
});

describe("parse_ContainsIntLiteral_Ok", () => {
  it("parses contains(5) — an int literal arg (model decides element-type validity)", () => {
    const dp = asBooleanAccessPath(
      assertOk(parseResultPredicate("result.data.codes.contains(5)")),
    );
    const seg = dp.segments[1] as ArrayAccessorSegment;
    expect(seg.literal).toEqual({ kind: "int", value: "5" });
  });
});

describe("parse_NestedQuantifierDistinctVars_Ok", () => {
  it('parses items.any(i => i.subs.any(j => j.x == "y")) with distinct vars', () => {
    const result = parseResultPredicate(
      'result.data.items.any(i => i.subs.any(j => j.x == "y"))',
    );
    // Proves the shadow guard is precise, not a blanket nested-quantifier ban.
    expect(result.ok).toBe(true);
  });
});

// ----------------------------------------------------------------
// Invalid — malformed
// ----------------------------------------------------------------

describe("parse_EmptyString_Reject", () => {
  it("rejects '' with resilience-predicate-malformed", () => {
    assertFail(parseResultPredicate(""), "resilience-predicate-malformed");
  });
});

describe("parse_WhitespaceOnly_Reject", () => {
  it("rejects whitespace-only with resilience-predicate-malformed", () => {
    assertFail(parseResultPredicate("   "), "resilience-predicate-malformed");
  });
});

describe("parse_UnrecognizedChar_Reject", () => {
  it("rejects a stray '@' with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.success @ true"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_UnterminatedString_Reject", () => {
  it("rejects an unterminated string literal with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate('result.errorCode == "OPEN'),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_DanglingRoot_Reject", () => {
  it("rejects 'result.' with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result."),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_BareResultNoField_Reject", () => {
  it("rejects 'result' with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_DanglingData_Reject", () => {
  it("rejects 'result.data.' with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data."),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_UnbalancedGroupParen_Reject", () => {
  it("rejects '(result.success == true' with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("(result.success == true"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_EmptyQuantifierBody_Reject", () => {
  it("rejects items.any(i => ) with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data.items.any(i => )"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_TrailingAnd_Reject", () => {
  it("rejects a trailing && with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.success == true &&"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_OrderedComparator_Reject", () => {
  it("rejects result.statusCode > 400 (no ordered comparators) with resilience-predicate-malformed", () => {
    // '>' is an unrecognized character → malformed (ordered comparators are excluded).
    assertFail(
      parseResultPredicate("result.statusCode > 400"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_MissingComparisonOp_Reject", () => {
  it("rejects result.success (an accessor with no operator) with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.success"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_ArrowWithoutQuantifier_Reject", () => {
  it("rejects a bare => with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data => x"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_AccessorStartsWithArrayName_Reject", () => {
  it("rejects result.data.count.x (count cannot start a path after data) — malformed", () => {
    // After "data." the first segment must be a plain field; a bare 'count' as
    // the leading data segment is malformed (nothing to count).
    assertFail(
      parseResultPredicate("result.data.count"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_NonNameAfterData_Reject", () => {
  it("rejects result.data.123 with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data.123"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_DotThenNonName_Reject", () => {
  it("rejects result.data.order.123 (non-name after a dot) with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data.order.123"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_EmptyInList_Reject", () => {
  it("rejects result.errorCode in () with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.errorCode in ()"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_InWithoutParen_Reject", () => {
  it('rejects result.errorCode in "X" (no paren after in) with resilience-predicate-malformed', () => {
    assertFail(
      parseResultPredicate('result.errorCode in "X"'),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_InListUnclosed_Reject", () => {
  it('rejects result.errorCode in ("A", "B" with resilience-predicate-malformed', () => {
    assertFail(
      parseResultPredicate('result.errorCode in ("A", "B"'),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_QuantifierNoElemVar_Reject", () => {
  it("rejects items.any(=> x) — no element variable — with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data.items.any(=> result.success == true)"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_QuantifierNoArrow_Reject", () => {
  it("rejects items.any(i i.x == 1) — missing arrow — with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data.items.any(i i.x == 1)"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_QuantifierUnclosed_Reject", () => {
  it("rejects items.any(i => i.x == 1 (missing ')') with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate('result.data.items.any(i => i.x == "1"'),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_ContainsNoLiteral_Reject", () => {
  it("rejects contains() — no literal — with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.data.codes.contains()"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_ContainsUnclosed_Reject", () => {
  it("rejects contains(\"X\" — missing ')' — with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate('result.data.codes.contains("X"'),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_RhsNotALiteral_Reject", () => {
  it("rejects result.success == foo (RHS is a name, not a literal) with resilience-predicate-malformed", () => {
    assertFail(
      parseResultPredicate("result.success == foo"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_TrailingTokenAfterExpression_Reject", () => {
  it("rejects a valid expression followed by trailing garbage with resilience-predicate-malformed", () => {
    // parseOr succeeds on the first comparison; the leftover token has no
    // joining operator → the top-level parse reports the trailing token.
    assertFail(
      parseResultPredicate("result.success == true result.success == false"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_BadRhsOfOr_Reject", () => {
  it("rejects a valid LHS || a malformed RHS — error propagates out of parseOr", () => {
    assertFail(
      parseResultPredicate("result.success == true || bogus"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_BadRhsOfAnd_Reject", () => {
  it("rejects a valid LHS && a malformed RHS — error propagates out of parseAnd", () => {
    assertFail(
      parseResultPredicate("result.success == true && bogus"),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_MalformedInsideGroup_Reject", () => {
  it("rejects (result.bogus == 1) — a malformed expression inside a group", () => {
    // The group's inner parse fails (unknown field), propagating out of the
    // parenthesized branch of parseComparison.
    assertFail(
      parseResultPredicate("(result.bogus == 1)"),
      "resilience-predicate-unknown-field",
    );
  });
});

describe("parse_BadSecondInListElement_Reject", () => {
  it('rejects result.errorCode in ("A", foo) — second element not a literal', () => {
    assertFail(
      parseResultPredicate('result.errorCode in ("A", foo)'),
      "resilience-predicate-malformed",
    );
  });
});

describe("parse_LiteralAtEndOfInput_Reject", () => {
  it("rejects result.success == (operator then end of input) — literal parse hits eof", () => {
    // Exercises the 'end of input' rendering when parseLiteral peeks the eof token.
    assertFail(
      parseResultPredicate("result.success =="),
      "resilience-predicate-malformed",
    );
  });
});

// ----------------------------------------------------------------
// Invalid — unknown accessor
// ----------------------------------------------------------------

describe("parse_UnknownEnvelopeField_Reject", () => {
  it("rejects result.bogus == 1 with resilience-predicate-unknown-field", () => {
    assertFail(
      parseResultPredicate("result.bogus == 1"),
      "resilience-predicate-unknown-field",
    );
  });
});

// ----------------------------------------------------------------
// Invalid — envelope type-mismatch (model-free)
// ----------------------------------------------------------------

describe("parse_SuccessVsString_Reject", () => {
  it('rejects result.success == "x" (bool vs string) with resilience-predicate-type-mismatch', () => {
    assertFail(
      parseResultPredicate('result.success == "x"'),
      "resilience-predicate-type-mismatch",
    );
  });
});

describe("parse_StatusCodeVsString_Reject", () => {
  it('rejects result.statusCode == "x" (int vs string) with resilience-predicate-type-mismatch', () => {
    assertFail(
      parseResultPredicate('result.statusCode == "x"'),
      "resilience-predicate-type-mismatch",
    );
  });
});

describe("parse_ErrorCodeVsInt_Reject", () => {
  it("rejects result.errorCode == 5 (string vs int) with resilience-predicate-type-mismatch", () => {
    assertFail(
      parseResultPredicate("result.errorCode == 5"),
      "resilience-predicate-type-mismatch",
    );
  });
});

describe("parse_CategoryVsInt_Reject", () => {
  it("rejects result.category == 7 (string vs int) with resilience-predicate-type-mismatch", () => {
    assertFail(
      parseResultPredicate("result.category == 7"),
      "resilience-predicate-type-mismatch",
    );
  });
});

describe("parse_CountVsString_Reject", () => {
  it('rejects result.data.items.count == "x" (int count vs string) with resilience-predicate-type-mismatch', () => {
    assertFail(
      parseResultPredicate('result.data.items.count == "x"'),
      "resilience-predicate-type-mismatch",
    );
  });
});

describe("parse_InListMixedTypes_Reject", () => {
  it('rejects result.statusCode in ("a", 2) (mixed) with resilience-predicate-type-mismatch', () => {
    // The first element fixes the list type; statusCode also makes "a" a mismatch.
    assertFail(
      parseResultPredicate('result.statusCode in (1, "a")'),
      "resilience-predicate-type-mismatch",
    );
  });
});

// ----------------------------------------------------------------
// Invalid — shadowed elemVar
// ----------------------------------------------------------------

describe("parse_ShadowedElemVar_Reject", () => {
  it('rejects items.any(i => i.subs.any(i => i.x == "y")) with resilience-predicate-shadowed-elem-var', () => {
    assertFail(
      parseResultPredicate(
        'result.data.items.any(i => i.subs.any(i => i.x == "y"))',
      ),
      "resilience-predicate-shadowed-elem-var",
    );
  });
});

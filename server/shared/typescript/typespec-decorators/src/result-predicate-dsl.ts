// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Pure recursive-descent parser for the @d2Resilience custom result-predicate
// DSL (the `retryWhen` / `failWhen` option strings). This is a SECOND, separate
// grammar from the pipeline DSL (resilience-dsl.ts) — it is a minimal
// result-expression language reaching the D2Result envelope AND the wrapped
// TOutput fields, NOT the policy-call pipeline grammar.
//
// Grammar (EBNF):
//   expression    := orExpr
//   orExpr        := andExpr ( "||" andExpr )*
//   andExpr       := comparison ( "&&" comparison )*
//   comparison    := accessor ( ( "==" | "!=" ) literal
//                             | "in" "(" literal ( "," literal )* ")" )
//                  | "(" orExpr ")"
//   accessor      := "result" "." field                 (top-level root)
//                  | <elemVar> "." dataPath              (sub-predicate root)
//   field         := "success" | "statusCode" | "errorCode" | "category"
//                  | "data" "." dataPath
//   dataPath      := pathSegment ( "." ( pathSegment | arrayAccessor ) )*
//   pathSegment   := identifier
//   arrayAccessor := "count"
//                  | ( "any" | "all" ) "(" elemVar "=>" subPredicate ")"
//                  | "contains" "(" literal ")"
//   subPredicate  := orExpr                              (rooted at <elemVar>)
//   literal       := stringLit | intLit | boolLit
//
// Operators: == != in() && || , grouping (), and the four array accessors
// (count / any / all / contains). No ordered comparators (< > <= >=) — those
// are rejected as malformed.
//
// Sub-predicate scoping: a nested quantifier that re-binds an identifier
// already in scope is a hard error (resilience-predicate-shadowed-elem-var).
//
// The parser is pure — it has no side effects and does NOT import TypeSpec
// types. Model-dependent checks (unknown output / element field, not-a-collection,
// terminal data type-mismatch) run later in $onValidate against the real TOutput
// graph (see predicate-model-walk.ts); the registry-value checks (unknown error
// code / category) run in the decorator-body validator. The parser emits ONLY
// the model-free diagnostics: malformed, unknown-field, shadowed-elem-var, and
// the envelope-arm type-mismatches.

// ----------------------------------------------------------------
// Diagnostic codes — single source shared with lib.ts catalog.
// When a code is added here it MUST be added to lib.ts too (and
// vice-versa). The catalog-integrity test guards against drift.
// ----------------------------------------------------------------

/**
 * Diagnostic codes emitted across the result-predicate DSL (the parser here
 * plus the decorator-body registry checks and the $onValidate model walk).
 * Maps 1:1 to entries in $lib.diagnostics — the drift guard enforces this.
 */
export type ResultPredicateDiagnosticCode =
  | "resilience-predicate-malformed"
  | "resilience-predicate-unknown-field"
  | "resilience-predicate-unknown-output-field"
  | "resilience-predicate-unknown-error-code"
  | "resilience-predicate-unknown-category"
  | "resilience-predicate-type-mismatch"
  | "resilience-predicate-not-a-collection"
  | "resilience-predicate-unknown-element-field"
  | "resilience-predicate-shadowed-elem-var";

// ----------------------------------------------------------------
// AST types — consumed by result-predicate validation and the
// cross-language emitter.
// ----------------------------------------------------------------

/** A literal value in a comparison. */
export interface LiteralNode {
  readonly kind: "string" | "int" | "bool";
  /** The string value as written; for `int` it parses to a number, for `bool` to true/false. */
  readonly value: string;
}

/** The four `result.<field>` envelope accessors (top-level root only). */
export type EnvelopeField = "success" | "statusCode" | "errorCode" | "category";

/** An accessor onto a D2Result envelope field (`result.success`, etc.). */
export interface EnvelopeAccessNode {
  readonly kind: "envelope";
  readonly field: EnvelopeField;
}

/**
 * One segment of a data path. Either a plain field (`order`, `customer`) or an
 * array accessor (`count` / `any` / `all` / `contains`). A field segment may be
 * followed by an array accessor; an array accessor is always terminal for its
 * collection (any/all recurse via a sub-predicate; count/contains yield a
 * scalar/boolean).
 */
export type PathSegment = FieldSegment | ArrayAccessorSegment;

/** A plain field on the current model node. */
export interface FieldSegment {
  readonly kind: "field";
  readonly name: string;
}

/** An array accessor applied to the prior (collection) segment. */
export interface ArrayAccessorSegment {
  readonly kind: "arrayAccessor";
  readonly accessor: "count" | "any" | "all" | "contains";
  /** Bound element variable for `any` / `all`; undefined for `count` / `contains`. */
  readonly elemVar?: string;
  /** Sub-predicate tree for `any` / `all`; undefined for `count` / `contains`. */
  readonly subPredicate?: PredicateNode;
  /** Literal argument for `contains`; undefined for the others. */
  readonly literal?: LiteralNode;
}

/**
 * A path through the wrapped TOutput. At the top level this is rooted at
 * `result.data`; inside a sub-predicate it is rooted at the bound element
 * variable (`elemVar`). The `root` records which.
 */
export interface DataPathNode {
  readonly kind: "dataPath";
  /** "data" at the top level (result.data.…); the elemVar name inside a sub-predicate. */
  readonly root: string;
  readonly segments: readonly PathSegment[];
}

/** Any accessor that can sit on the left of a comparison. */
export type AccessNode = EnvelopeAccessNode | DataPathNode;

/** A comparison node: `<access> == <lit>`, `<access> != <lit>`, or `<access> in (<lits>)`. */
export interface ComparisonNode {
  readonly kind: "comparison";
  readonly access: AccessNode;
  readonly op: "==" | "!=" | "in";
  /** A single literal for ==/!=; an array of ≥1 literals for `in`. */
  readonly rhs: LiteralNode | readonly LiteralNode[];
}

/**
 * A data path that terminates in a boolean-valued array accessor (`any` / `all`
 * / `contains`) and therefore stands alone as a complete boolean predicate term
 * — no trailing comparison operator. (`count` yields an int and uses a
 * ComparisonNode instead.)
 */
export interface BooleanAccessNode {
  readonly kind: "booleanAccess";
  readonly access: DataPathNode;
}

/** A boolean combination of two predicate sub-trees. */
export interface BoolNode {
  readonly kind: "bool";
  readonly op: "&&" | "||";
  readonly left: PredicateNode;
  readonly right: PredicateNode;
}

/** The top of any predicate (sub-)expression. */
export type PredicateNode = BoolNode | ComparisonNode | BooleanAccessNode;

/** Discriminated-union result of a parse attempt. */
export type ResultPredicateParseResult =
  | { readonly ok: true; readonly root: PredicateNode }
  | {
      readonly ok: false;
      readonly errors: readonly ResultPredicateParseError[];
    };

/** A single parser error — maps to one $lib.reportDiagnostic call. */
export interface ResultPredicateParseError {
  readonly code: ResultPredicateDiagnosticCode;
  readonly message: string;
  /** Character offset into the expression string (for future precision squiggles). */
  readonly offset?: number;
}

// ----------------------------------------------------------------
// Envelope-field literal-type rules (model-free)
// ----------------------------------------------------------------

/** Expected literal kind per envelope field — drives parser-side type-mismatch. */
const ENVELOPE_FIELD_TYPE: Record<EnvelopeField, "bool" | "int" | "string"> = {
  success: "bool",
  statusCode: "int",
  errorCode: "string",
  category: "string",
};

const ENVELOPE_FIELDS = new Set<string>(Object.keys(ENVELOPE_FIELD_TYPE));
const ARRAY_ACCESSORS = new Set(["count", "any", "all", "contains"]);

// ----------------------------------------------------------------
// Lexer
// ----------------------------------------------------------------

type TokenKind =
  | "name"
  | "number"
  | "bool"
  | "string-literal"
  | "dot"
  | "comma"
  | "lparen"
  | "rparen"
  | "eq"
  | "neq"
  | "and"
  | "or"
  | "arrow"
  | "eof";

interface Token {
  readonly kind: TokenKind;
  readonly value: string;
  readonly offset: number;
}

/**
 * Tokenize the expression. Returns undefined on an unrecognized character or an
 * unterminated string literal (both surface as `resilience-predicate-malformed`).
 */
function tokenize(input: string): Token[] | undefined {
  const tokens: Token[] = [];
  let i = 0;
  while (i < input.length) {
    const ch = input[i]!;

    if (/\s/.test(ch)) {
      i++;
      continue;
    }

    if (ch === "(") {
      tokens.push({ kind: "lparen", value: "(", offset: i });
      i++;
    } else if (ch === ")") {
      tokens.push({ kind: "rparen", value: ")", offset: i });
      i++;
    } else if (ch === ",") {
      tokens.push({ kind: "comma", value: ",", offset: i });
      i++;
    } else if (ch === ".") {
      tokens.push({ kind: "dot", value: ".", offset: i });
      i++;
    } else if (ch === "=" && input[i + 1] === "=") {
      tokens.push({ kind: "eq", value: "==", offset: i });
      i += 2;
    } else if (ch === "!" && input[i + 1] === "=") {
      tokens.push({ kind: "neq", value: "!=", offset: i });
      i += 2;
    } else if (ch === "&" && input[i + 1] === "&") {
      tokens.push({ kind: "and", value: "&&", offset: i });
      i += 2;
    } else if (ch === "|" && input[i + 1] === "|") {
      tokens.push({ kind: "or", value: "||", offset: i });
      i += 2;
    } else if (ch === "=" && input[i + 1] === ">") {
      tokens.push({ kind: "arrow", value: "=>", offset: i });
      i += 2;
    } else if (/[a-zA-Z_]/.test(ch)) {
      let j = i;
      while (j < input.length && /[a-zA-Z0-9_]/.test(input[j]!)) j++;
      const word = input.slice(i, j);

      if (word === "true" || word === "false")
        tokens.push({ kind: "bool", value: word, offset: i });
      else tokens.push({ kind: "name", value: word, offset: i });

      i = j;
    } else if (
      /[0-9]/.test(ch) ||
      (ch === "-" && /[0-9]/.test(input[i + 1] ?? ""))
    ) {
      let j = ch === "-" ? i + 1 : i;
      while (j < input.length && /[0-9]/.test(input[j]!)) j++;
      tokens.push({ kind: "number", value: input.slice(i, j), offset: i });
      i = j;
    } else if (ch === '"') {
      let j = i + 1;
      while (j < input.length && input[j] !== '"') j++;

      if (j >= input.length) return undefined; // unterminated string literal

      tokens.push({
        kind: "string-literal",
        value: input.slice(i + 1, j),
        offset: i,
      });
      i = j + 1;
    } else {
      return undefined; // unrecognized character → malformed
    }
  }

  tokens.push({ kind: "eof", value: "", offset: i });
  return tokens;
}

// ----------------------------------------------------------------
// Recursive-descent parser
// ----------------------------------------------------------------

class Parser {
  private pos = 0;
  private readonly tokens: Token[];
  readonly errors: ResultPredicateParseError[] = [];

  constructor(tokens: Token[]) {
    this.tokens = tokens;
  }

  private peek(): Token {
    // Clamp to the eof sentinel: once pos reaches the tail, every peek returns
    // eof, so an over-advancing consume() is harmless (and the branch-free
    // consume() below relies on this).
    return this.tokens[Math.min(this.pos, this.tokens.length - 1)]!;
  }

  /**
   * The kind of the token one position ahead. pos+1 is always in-bounds when a
   * lookahead is needed (a name token is current, so the eof sentinel at minimum
   * follows), so the non-null assertion is safe by construction.
   */
  private peekNextKind(): TokenKind {
    return this.tokens[this.pos + 1]!.kind;
  }

  private consume(): Token {
    const t = this.peek();
    this.pos++;
    return t;
  }

  private fail(message: string, offset: number): void {
    this.errors.push({
      code: "resilience-predicate-malformed",
      message,
      offset,
    });
  }

  /** Entry: parse a full top-level expression rooted at `result`. */
  parse(): PredicateNode | undefined {
    const node = this.parseOr(new Set(), "result");
    if (!node) return undefined;

    const remaining = this.peek();
    if (remaining.kind !== "eof") {
      this.fail(
        `unexpected token '${remaining.value}' after expression`,
        remaining.offset,
      );
      return undefined;
    }

    return node;
  }

  /** orExpr := andExpr ( "||" andExpr )* */
  private parseOr(scope: Set<string>, root: string): PredicateNode | undefined {
    let left = this.parseAnd(scope, root);
    if (!left) return undefined;

    while (this.peek().kind === "or") {
      this.consume();
      const right = this.parseAnd(scope, root);
      if (!right) return undefined;

      left = { kind: "bool", op: "||", left, right };
    }

    return left;
  }

  /** andExpr := comparison ( "&&" comparison )* */
  private parseAnd(
    scope: Set<string>,
    root: string,
  ): PredicateNode | undefined {
    let left = this.parseComparison(scope, root);
    if (!left) return undefined;

    while (this.peek().kind === "and") {
      this.consume();
      const right = this.parseComparison(scope, root);
      if (!right) return undefined;

      left = { kind: "bool", op: "&&", left, right };
    }

    return left;
  }

  /** comparison := accessor ( eq/neq lit | "in" "(" lits ")" ) | "(" orExpr ")" */
  private parseComparison(
    scope: Set<string>,
    root: string,
  ): PredicateNode | undefined {
    if (this.peek().kind === "lparen") {
      this.consume();
      const inner = this.parseOr(scope, root);
      if (!inner) return undefined;

      if (this.peek().kind !== "rparen") {
        this.fail("missing ')' in grouped expression", this.peek().offset);
        return undefined;
      }

      this.consume();
      return inner;
    }

    const access = this.parseAccessor(scope, root);
    if (!access) return undefined;

    // A data path ending in a boolean array accessor (any/all/contains) is a
    // complete boolean term — it takes NO trailing comparison operator.
    if (access.kind === "dataPath") {
      // parseDataPath always pushes ≥1 segment, so the last index is in bounds.
      const last = access.segments[access.segments.length - 1]!;
      if (
        last.kind === "arrayAccessor" &&
        (last.accessor === "any" ||
          last.accessor === "all" ||
          last.accessor === "contains")
      )
        return { kind: "booleanAccess", access };
    }

    const opTok = this.peek();

    if (opTok.kind === "eq" || opTok.kind === "neq") {
      this.consume();
      const lit = this.parseLiteral();
      if (!lit) return undefined;

      this.checkAccessLiteralType(access, lit, opTok.offset);
      return {
        kind: "comparison",
        access,
        op: opTok.kind === "eq" ? "==" : "!=",
        rhs: lit,
      };
    }

    if (opTok.kind === "name" && opTok.value === "in") {
      this.consume();
      const lits = this.parseInList();
      if (!lits) return undefined;

      for (const lit of lits)
        this.checkAccessLiteralType(access, lit, opTok.offset);
      this.checkListHomogeneity(lits, opTok.offset);
      return { kind: "comparison", access, op: "in", rhs: lits };
    }

    this.fail(
      `expected a comparison operator (==, !=, in) but found '${opTok.value || "end of input"}'`,
      opTok.offset,
    );
    return undefined;
  }

  /** "(" literal ( "," literal )* ")" — the `in` membership list. */
  private parseInList(): LiteralNode[] | undefined {
    if (this.peek().kind !== "lparen") {
      this.fail("expected '(' after 'in'", this.peek().offset);
      return undefined;
    }

    this.consume();
    const lits: LiteralNode[] = [];

    if (this.peek().kind === "rparen") {
      this.fail("'in (...)' requires at least one value", this.peek().offset);
      return undefined;
    }

    while (true) {
      const lit = this.parseLiteral();
      if (!lit) return undefined;

      lits.push(lit);

      if (this.peek().kind === "comma") {
        this.consume();
        continue;
      }

      break;
    }

    if (this.peek().kind !== "rparen") {
      this.fail("missing ')' in 'in (...)' list", this.peek().offset);
      return undefined;
    }

    this.consume();
    return lits;
  }

  /** accessor := root "." (envelopeField | dataPath) . */
  private parseAccessor(
    scope: Set<string>,
    root: string,
  ): AccessNode | undefined {
    const rootTok = this.peek();
    if (rootTok.kind !== "name" || rootTok.value !== root) {
      this.fail(
        `expected '${root}' but found '${rootTok.value || "end of input"}'`,
        rootTok.offset,
      );
      return undefined;
    }

    this.consume();

    if (this.peek().kind !== "dot") {
      this.fail(`expected '.' after '${root}'`, this.peek().offset);
      return undefined;
    }

    this.consume();

    const firstTok = this.peek();
    if (firstTok.kind !== "name") {
      this.fail(`expected a field name after '${root}.'`, firstTok.offset);
      return undefined;
    }

    // Top-level root: the first segment is an envelope field OR "data".
    if (root === "result") {
      if (firstTok.value === "data") {
        this.consume();

        if (this.peek().kind !== "dot") {
          this.fail("expected '.' after 'result.data'", this.peek().offset);
          return undefined;
        }

        this.consume();
        return this.parseDataPath(scope, "data");
      }

      if (ENVELOPE_FIELDS.has(firstTok.value)) {
        this.consume();
        return { kind: "envelope", field: firstTok.value as EnvelopeField };
      }

      this.errors.push({
        code: "resilience-predicate-unknown-field",
        message:
          `'result.${firstTok.value}' is not a recognized accessor — ` +
          "expected one of: success, statusCode, errorCode, category, data",
        offset: firstTok.offset,
      });
      return undefined;
    }

    // Sub-predicate root (bound elemVar): the whole path is a data path; the
    // parser is now positioned at the first field segment (the dot after the
    // elemVar was consumed above).
    return this.parseDataPath(scope, root);
  }

  /**
   * dataPath := pathSegment ( "." ( pathSegment | arrayAccessor ) )*
   * Entered with the parser positioned AT the first field segment — the leading
   * root keyword and its trailing dot were already consumed by parseAccessor
   * (uniformly for both the "data" root and an elemVar root).
   */
  private parseDataPath(
    scope: Set<string>,
    root: string,
  ): DataPathNode | undefined {
    const segments: PathSegment[] = [];

    // First segment is always a plain field (a path cannot start with an
    // array accessor — that would require a preceding collection).
    const first = this.parsePathField(root, segments.length);
    if (!first) return undefined;

    segments.push(first);

    while (this.peek().kind === "dot") {
      this.consume();
      const seg = this.parseSegmentAfterDot(scope, root, segments.length);
      if (!seg) return undefined;

      segments.push(seg);
    }

    return { kind: "dataPath", root, segments };
  }

  /** A bare field segment (identifier that is not an array accessor name). */
  private parsePathField(
    root: string,
    index: number,
  ): FieldSegment | undefined {
    const tok = this.peek();
    if (tok.kind !== "name") {
      this.fail(
        `expected a field name in the data path of '${root}'`,
        tok.offset,
      );
      return undefined;
    }

    // A bare array-accessor name as the FIRST segment is malformed (nothing to
    // apply it to). `count`/`any`/`all`/`contains` are only valid AFTER a dot
    // following a field — handled in parseSegmentAfterDot.
    if (index === 0 && ARRAY_ACCESSORS.has(tok.value)) {
      this.fail(
        `'${tok.value}' cannot start a data path — it must follow a collection field`,
        tok.offset,
      );
      return undefined;
    }

    this.consume();
    return { kind: "field", name: tok.value };
  }

  /** After a dot: either an array accessor (count/any/all/contains) or a plain field. */
  private parseSegmentAfterDot(
    scope: Set<string>,
    root: string,
    index: number,
  ): PathSegment | undefined {
    const tok = this.peek();
    if (tok.kind !== "name") {
      this.fail(
        `expected a field name or array accessor after '.' in the data path of '${root}'`,
        tok.offset,
      );
      return undefined;
    }

    // count — but only when not actually called as count(...) (count takes no args)
    if (tok.value === "count" && this.peekNextKind() !== "lparen") {
      this.consume();
      return { kind: "arrayAccessor", accessor: "count" };
    }

    if (
      (tok.value === "any" || tok.value === "all") &&
      this.peekNextKind() === "lparen"
    )
      return this.parseQuantifier(scope, tok.value);

    if (tok.value === "contains" && this.peekNextKind() === "lparen")
      return this.parseContains();

    // Otherwise it is a plain field segment.
    return this.parsePathField(root, index);
  }

  /** ( "any" | "all" ) "(" elemVar "=>" subPredicate ")" . */
  private parseQuantifier(
    scope: Set<string>,
    accessor: "any" | "all",
  ): ArrayAccessorSegment | undefined {
    this.consume(); // accessor name
    this.consume(); // lparen (peeked by caller)

    const elemTok = this.peek();
    if (elemTok.kind !== "name") {
      this.fail(
        `expected an element variable in '${accessor}(...)'`,
        elemTok.offset,
      );
      return undefined;
    }

    // A nested quantifier may not re-bind a name already in scope.
    if (scope.has(elemTok.value)) {
      this.errors.push({
        code: "resilience-predicate-shadowed-elem-var",
        message:
          `element variable '${elemTok.value}' shadows an outer one — ` +
          "use a distinct name in nested quantifiers",
        offset: elemTok.offset,
      });
      return undefined;
    }

    this.consume(); // elemVar

    if (this.peek().kind !== "arrow") {
      this.fail(
        `expected '=>' after the element variable in '${accessor}(...)'`,
        this.peek().offset,
      );
      return undefined;
    }

    this.consume(); // arrow

    const innerScope = new Set(scope);
    innerScope.add(elemTok.value);
    const sub = this.parseOr(innerScope, elemTok.value);
    if (!sub) return undefined;

    if (this.peek().kind !== "rparen") {
      this.fail(`missing ')' to close '${accessor}(...)'`, this.peek().offset);
      return undefined;
    }

    this.consume(); // rparen
    return {
      kind: "arrayAccessor",
      accessor,
      elemVar: elemTok.value,
      subPredicate: sub,
    };
  }

  /** "contains" "(" literal ")" . */
  private parseContains(): ArrayAccessorSegment | undefined {
    this.consume(); // "contains"
    this.consume(); // lparen (peeked by caller)

    const lit = this.parseLiteral();
    if (!lit) return undefined;

    if (this.peek().kind !== "rparen") {
      this.fail("missing ')' to close 'contains(...)'", this.peek().offset);
      return undefined;
    }

    this.consume(); // rparen
    return { kind: "arrayAccessor", accessor: "contains", literal: lit };
  }

  /** literal := stringLit | intLit | boolLit . */
  private parseLiteral(): LiteralNode | undefined {
    const tok = this.peek();

    if (tok.kind === "string-literal") {
      this.consume();
      return { kind: "string", value: tok.value };
    }

    if (tok.kind === "number") {
      this.consume();
      return { kind: "int", value: tok.value };
    }

    if (tok.kind === "bool") {
      this.consume();
      return { kind: "bool", value: tok.value };
    }

    this.fail(
      `expected a literal (string, integer, or boolean) but found '${tok.value || "end of input"}'`,
      tok.offset,
    );
    return undefined;
  }

  // --------------------------------------------------------------
  // Model-free type checks
  // --------------------------------------------------------------

  /**
   * Check an access node against a literal type for the model-free arms:
   * the four envelope fields and the `.count` accessor (an int). Data-path
   * terminals other than `.count` need the model and are checked in $onValidate.
   */
  private checkAccessLiteralType(
    access: AccessNode,
    lit: LiteralNode,
    offset: number,
  ): void {
    if (access.kind === "envelope") {
      const expected = ENVELOPE_FIELD_TYPE[access.field];
      if (lit.kind !== expected)
        this.pushTypeMismatch(`result.${access.field}`, expected, lit, offset);

      return;
    }

    // dataPath: only `.count` is model-free (it is always an int).
    // parseDataPath always pushes ≥1 segment, so the last index is in bounds.
    const last = access.segments[access.segments.length - 1]!;
    if (
      last.kind === "arrayAccessor" &&
      last.accessor === "count" &&
      lit.kind !== "int"
    )
      this.pushTypeMismatch("count", "int", lit, offset);
  }

  /** Every literal in an `in (...)` list must share one kind. */
  private checkListHomogeneity(
    lits: readonly LiteralNode[],
    offset: number,
  ): void {
    const first = lits[0]!.kind;
    if (lits.some((l) => l.kind !== first))
      this.errors.push({
        code: "resilience-predicate-type-mismatch",
        message: "all values in an 'in (...)' list must share one type",
        offset,
      });
  }

  private pushTypeMismatch(
    accessLabel: string,
    expected: string,
    lit: LiteralNode,
    offset: number,
  ): void {
    this.errors.push({
      code: "resilience-predicate-type-mismatch",
      message: `'${accessLabel}' expects a ${expected} literal but got a ${lit.kind} ('${lit.value}')`,
      offset,
    });
  }
}

// ----------------------------------------------------------------
// Public parse entry point
// ----------------------------------------------------------------

/**
 * Parse a @d2Resilience result-predicate string (a `retryWhen` / `failWhen`
 * value) into an AST.
 *
 * Returns `{ ok: true, root }` on success or `{ ok: false, errors }` on
 * failure. Pure — no side effects, no TypeSpec imports. The emitter
 * re-parses the stored raw string to walk the AST; $onValidate re-parses it to
 * run the model-graph checks.
 */
export function parseResultPredicate(expr: string): ResultPredicateParseResult {
  const trimmed = expr.trim();
  if (trimmed.length === 0)
    return {
      ok: false,
      errors: [
        {
          code: "resilience-predicate-malformed",
          message: "predicate expression must not be empty",
          offset: 0,
        },
      ],
    };

  const tokens = tokenize(trimmed);
  if (tokens === undefined)
    return {
      ok: false,
      errors: [
        {
          code: "resilience-predicate-malformed",
          message:
            "predicate expression contains unrecognized characters or an unterminated string",
          offset: 0,
        },
      ],
    };

  const parser = new Parser(tokens);
  const root = parser.parse();

  if (parser.errors.length > 0) return { ok: false, errors: parser.errors };

  // parse() always either returns a node or pushes at least one error; because
  // parser.errors.length === 0, root is always defined here.
  return { ok: true, root: root! };
}

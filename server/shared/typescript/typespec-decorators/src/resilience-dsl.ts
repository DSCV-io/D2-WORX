// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Pure recursive-descent parser for the @d2Resilience pipeline-expression DSL.
//
// Grammar (EBNF-ish):
//   expression   := policyCall
//   policyCall   := policyName "(" argList? ")"
//   policyName   := "retry" | "circuitBreaker" | "singleflight"
//   argList      := arg ("," arg)*
//   arg          := namedArg | positionalArg | policyCall
//   namedArg     := identifier ":" literal
//   positionalArg:= literal
//   literal      := number | duration | boolean
//   number       := /-?[0-9]+/          (integers only)
//   duration     := /[0-9]+(ms|s)/      (normalized to the tunable's native unit)
//   boolean      := "true" | "false"
//   identifier   := /[a-zA-Z][a-zA-Z0-9]*/
//
// Nesting rule: each policy wraps at most one inner policy call (linear stack).
// Positional args must precede named args. singleflight accepts no tunables.
//
// Tunable provenance: values mirror D2.Shared.Resilience (RetryDefaults,
// CircuitBreakerOptions, Singleflight) in server/shared/dotnet/resilience/.
// The emitter maps cooldownSeconds → TimeSpan.FromSeconds(value).

// ----------------------------------------------------------------
// Diagnostic codes — single source shared with lib.ts catalog.
// When a code is added here it MUST be added to lib.ts too (and
// vice-versa). The catalog-integrity test guards against drift.
// ----------------------------------------------------------------

/** Diagnostic codes emitted by the DSL parser.  Maps 1:1 to entries in $lib.diagnostics. */
export type ResilienceDiagnosticCode =
  | "resilience-malformed"
  | "resilience-unknown-policy"
  | "resilience-unknown-arg"
  | "resilience-bad-arg"
  | "resilience-multiple-inner"
  | "resilience-positional-after-named";

// ----------------------------------------------------------------
// AST types — consumed by resilience pipeline-expression validation now
// and the emitter later.
// ----------------------------------------------------------------

/** A single node in the linear resilience-policy composition tree. */
export interface ResiliencePolicyNode {
  readonly policy: "retry" | "circuitBreaker" | "singleflight";
  /**
   * Tunable values resolved and normalized (durations → ms or seconds per tunable).
   *
   * **Sparse contract (emitter-load-bearing):** only explicitly-provided tunable keys are
   * present. An absent key means "use the C# library default" — the emitter must omit that
   * property from the generated options constructor, NOT emit zero or false.
   * Example: `retry()` → `{}` (all defaults); `retry(3)` → `{ maxAttempts: 3 }`.
   */
  readonly tunables: Readonly<Record<string, number | boolean>>;
  /** The wrapped inner policy, if any.  undefined for leaf nodes. */
  readonly inner?: ResiliencePolicyNode;
}

/** Discriminated-union result of a parse attempt. */
export type ResilienceParseResult =
  | { readonly ok: true; readonly root: ResiliencePolicyNode }
  | { readonly ok: false; readonly errors: readonly ResilienceParseError[] };

/** A single parser error — maps to one $lib.reportDiagnostic call. */
export interface ResilienceParseError {
  readonly code: ResilienceDiagnosticCode;
  readonly message: string;
  /** Character offset into the expression string (for future precision squiggles). */
  readonly offset?: number;
}

// ----------------------------------------------------------------
// Per-policy tunable schemas (mirrors D2.Shared.Resilience)
// ----------------------------------------------------------------

type TunableKind = "int" | "duration-ms" | "duration-s" | "bool";

interface TunableSpec {
  readonly kind: TunableKind;
  /** Minimum allowed value (inclusive), for numeric tunables. */
  readonly min?: number;
  /** Canonical name in the emitted tunables record. */
  readonly canonicalName: string;
}

/** Map from DSL tunable name (or alias) → spec for that tunable. */
type TunableSchema = Record<string, TunableSpec>;

// Retry: positional order = maxAttempts, baseDelayMs, backoffMultiplier, maxDelayMs, jitter
const RETRY_TUNABLES: TunableSchema = {
  maxAttempts: { kind: "int", min: 1, canonicalName: "maxAttempts" },
  baseDelayMs: { kind: "duration-ms", min: 0, canonicalName: "baseDelayMs" },
  baseDelay: { kind: "duration-ms", min: 0, canonicalName: "baseDelayMs" },
  backoffMultiplier: {
    kind: "int",
    min: 1,
    canonicalName: "backoffMultiplier",
  },
  maxDelayMs: { kind: "duration-ms", min: 0, canonicalName: "maxDelayMs" },
  maxDelay: { kind: "duration-ms", min: 0, canonicalName: "maxDelayMs" },
  jitter: { kind: "bool", canonicalName: "jitter" },
};

const RETRY_POSITIONAL_ORDER: string[] = [
  "maxAttempts",
  "baseDelayMs",
  "backoffMultiplier",
  "maxDelayMs",
  "jitter",
];

// circuitBreaker: cooldownSeconds → emitter maps to TimeSpan.FromSeconds(value)
const CIRCUIT_BREAKER_TUNABLES: TunableSchema = {
  failureThreshold: { kind: "int", min: 1, canonicalName: "failureThreshold" },
  threshold: { kind: "int", min: 1, canonicalName: "failureThreshold" },
  cooldownSeconds: {
    kind: "duration-s",
    min: 0,
    canonicalName: "cooldownSeconds",
  },
  cooldown: { kind: "duration-s", min: 0, canonicalName: "cooldownSeconds" },
};

const CIRCUIT_BREAKER_POSITIONAL_ORDER: string[] = [
  "failureThreshold",
  "cooldownSeconds",
];

// singleflight: zero tunables
const SINGLEFLIGHT_TUNABLES: TunableSchema = {};
const SINGLEFLIGHT_POSITIONAL_ORDER: string[] = [];

interface PolicyMeta {
  readonly tunables: TunableSchema;
  readonly positionalOrder: string[];
}

const POLICY_META: Record<string, PolicyMeta> = {
  retry: { tunables: RETRY_TUNABLES, positionalOrder: RETRY_POSITIONAL_ORDER },
  circuitBreaker: {
    tunables: CIRCUIT_BREAKER_TUNABLES,
    positionalOrder: CIRCUIT_BREAKER_POSITIONAL_ORDER,
  },
  singleflight: {
    tunables: SINGLEFLIGHT_TUNABLES,
    positionalOrder: SINGLEFLIGHT_POSITIONAL_ORDER,
  },
};

const KNOWN_POLICIES = new Set(Object.keys(POLICY_META));

// ----------------------------------------------------------------
// Lexer
// ----------------------------------------------------------------

type TokenKind =
  | "name"
  | "number"
  | "duration"
  | "bool"
  | "string-literal"
  | "colon"
  | "comma"
  | "lparen"
  | "rparen"
  | "eof";

interface Token {
  readonly kind: TokenKind;
  readonly value: string;
  readonly offset: number;
}

function tokenize(input: string): Token[] | null {
  const tokens: Token[] = [];
  let i = 0;
  while (i < input.length) {
    // Skip whitespace
    if (/\s/.test(input[i]!)) {
      i++;
      continue;
    }
    const ch = input[i]!;
    if (ch === "(") {
      tokens.push({ kind: "lparen", value: "(", offset: i });
      i++;
    } else if (ch === ")") {
      tokens.push({ kind: "rparen", value: ")", offset: i });
      i++;
    } else if (ch === ",") {
      tokens.push({ kind: "comma", value: ",", offset: i });
      i++;
    } else if (ch === ":") {
      tokens.push({ kind: "colon", value: ":", offset: i });
      i++;
    } else if (/[a-zA-Z]/.test(ch)) {
      let j = i;
      while (j < input.length && /[a-zA-Z0-9]/.test(input[j]!)) j++;
      const word = input.slice(i, j);
      if (word === "true" || word === "false")
        tokens.push({ kind: "bool", value: word, offset: i });
      else tokens.push({ kind: "name", value: word, offset: i });
      i = j;
    } else if (/[0-9]/.test(ch)) {
      let j = i;
      while (j < input.length && /[0-9]/.test(input[j]!)) j++;
      const numStr = input.slice(i, j);
      // Check for duration suffix
      if (j < input.length && input[j] === "m" && input[j + 1] === "s") {
        tokens.push({ kind: "duration", value: numStr + "ms", offset: i });
        i = j + 2;
      } else if (j < input.length && input[j] === "s") {
        tokens.push({ kind: "duration", value: numStr + "s", offset: i });
        i = j + 1;
      } else {
        tokens.push({ kind: "number", value: numStr, offset: i });
        i = j;
      }
    } else if (ch === '"') {
      // Quoted string — not valid in the DSL grammar; lex it to produce a
      // meaningful error (bad-arg: string where int expected).
      let j = i + 1;
      while (j < input.length && input[j] !== '"') j++;
      const content = input.slice(i + 1, j);
      tokens.push({ kind: "string-literal", value: content, offset: i });
      i = j + 1;
    } else {
      // Unrecognized character → signal malformed
      return null;
    }
  }
  tokens.push({ kind: "eof", value: "", offset: i });
  return tokens;
}

// ----------------------------------------------------------------
// Recursive-descent parser
// ----------------------------------------------------------------

/** Raw token value captured before binding / type-checking. */
interface RawArg {
  readonly kind: TokenKind;
  readonly value: string;
}

class Parser {
  private pos = 0;
  private readonly tokens: Token[];
  readonly errors: ResilienceParseError[] = [];

  constructor(tokens: Token[]) {
    this.tokens = tokens;
  }

  private peek(): Token {
    // pos is always within [0, tokens.length-1]; the eof sentinel is always present.
    return this.tokens[this.pos]!;
  }

  private consume(): Token {
    const t = this.tokens[this.pos]!;
    if (t.kind !== "eof") this.pos++;
    return t;
  }

  private expect(kind: TokenKind): Token | undefined {
    const t = this.peek();
    if (t.kind === kind) return this.consume();
    return undefined;
  }

  parse(): ResiliencePolicyNode | undefined {
    const node = this.parsePolicyCall();
    if (!node) return undefined;
    const remaining = this.peek();
    if (remaining.kind !== "eof") {
      this.errors.push({
        code: "resilience-malformed",
        message: `unexpected token '${remaining.value}' after expression`,
        offset: remaining.offset,
      });
      return undefined;
    }
    return node;
  }

  parsePolicyCall(): ResiliencePolicyNode | undefined {
    const nameTok = this.peek();
    if (nameTok.kind !== "name") {
      this.errors.push({
        code: "resilience-malformed",
        message: "expected a policy name",
        offset: nameTok.offset,
      });
      return undefined;
    }
    this.consume();
    const policyName = nameTok.value;

    if (!KNOWN_POLICIES.has(policyName)) {
      this.errors.push({
        code: "resilience-unknown-policy",
        message:
          `unknown resilience policy '${policyName}' — ` +
          "expected one of: retry, circuitBreaker, singleflight",
        offset: nameTok.offset,
      });
      // Attempt to consume the rest of the call for better subsequent error messages
      if (this.peek().kind === "lparen") {
        this.consume();
        this.skipArgList();
        this.expect("rparen");
      }
      return undefined;
    }

    const lparen = this.expect("lparen");
    if (!lparen) {
      this.errors.push({
        code: "resilience-malformed",
        message: `expected '(' after policy name '${policyName}'`,
        offset: this.peek().offset,
      });
      return undefined;
    }

    const meta = POLICY_META[policyName]!;
    const tunables: Record<string, number | boolean> = {};
    let inner: ResiliencePolicyNode | undefined;
    let innerCount = 0;
    let positionalIndex = 0;
    let seenNamed = false;

    if (this.peek().kind !== "rparen") {
      // Parse arg list
      while (true) {
        const tok = this.peek();

        // Nested policy call?
        if (tok.kind === "name" && KNOWN_POLICIES.has(tok.value)) {
          const next = this.tokens[this.pos + 1];
          if (next?.kind === "lparen") {
            // Inner policy call
            if (policyName === "singleflight") {
              this.errors.push({
                code: "resilience-unknown-arg",
                message: `'singleflight' has no tunable '${tok.value}'`,
                offset: tok.offset,
              });
              this.parsePolicyCall(); // consume to keep parse going
            } else {
              innerCount++;
              if (innerCount > 1) {
                this.errors.push({
                  code: "resilience-multiple-inner",
                  message:
                    `'${policyName}' wraps more than one inner policy — ` +
                    "a resilience pipeline is a linear stack",
                  offset: nameTok.offset,
                });
                this.parsePolicyCall(); // consume
              } else {
                inner = this.parsePolicyCall();
              }
            }
            // fall through to comma check
          } else {
            // Known policy name without '()' — this is always an error.
            // A bare policy name used as a positional arg is never a valid
            // literal; the author almost certainly forgot the parens.
            const bareTok = this.consume();
            this.errors.push({
              code: "resilience-malformed",
              message: `bare policy name '${bareTok.value}' — did you mean '${bareTok.value}()'?`,
              offset: bareTok.offset,
            });
          }
        } else if (policyName === "singleflight") {
          // singleflight takes no args at all
          const argTok = this.consume();
          // Skip a colon-value pair if present
          if (this.peek().kind === "colon") {
            this.consume();
            this.consume(); // value
          }
          this.errors.push({
            code: "resilience-unknown-arg",
            message: `'singleflight' has no tunable '${argTok.value}'`,
            offset: argTok.offset,
          });
        } else if (tok.kind === "name" && !KNOWN_POLICIES.has(tok.value)) {
          // Named arg: name ":" literal
          const next = this.tokens[this.pos + 1];
          if (next?.kind === "colon") {
            const nameTok2 = this.consume();
            this.consume(); // colon
            const valTok = this.consume();
            const dsName = nameTok2.value;
            seenNamed = true;
            const spec = meta.tunables[dsName];
            if (!spec) {
              this.errors.push({
                code: "resilience-unknown-arg",
                message: `'${policyName}' has no tunable '${dsName}'`,
                offset: nameTok2.offset,
              });
            } else {
              const validated = this.validateTunableValue(
                policyName,
                spec,
                { kind: valTok.kind, value: valTok.value },
                valTok.offset,
              );
              if (validated !== undefined)
                tunables[spec.canonicalName] = validated;
            }
          } else {
            // Name without colon — treat as positional with bad type (a bare name)
            const badTok = this.consume();
            this.errors.push({
              code: "resilience-malformed",
              message: `unexpected identifier '${badTok.value}' in argument list of '${policyName}'`,
              offset: badTok.offset,
            });
          }
        } else {
          // Positional literal: number, duration, bool, string-literal
          const litKinds: Set<TokenKind> = new Set([
            "number",
            "duration",
            "bool",
            "string-literal",
          ]);
          if (litKinds.has(tok.kind)) {
            if (seenNamed) {
              const argTok = this.consume();
              this.errors.push({
                code: "resilience-positional-after-named",
                message:
                  `'${policyName}' has a positional argument after a named one — ` +
                  "positional args must come first",
                offset: argTok.offset,
              });
            } else {
              const valTok = this.consume();
              const canonicalKey = meta.positionalOrder[positionalIndex];
              if (!canonicalKey) {
                this.errors.push({
                  code: "resilience-unknown-arg",
                  message: `'${policyName}' has no positional slot ${positionalIndex + 1}`,
                  offset: valTok.offset,
                });
              } else {
                const spec = meta.tunables[canonicalKey]!;
                const validated = this.validateTunableValue(
                  policyName,
                  spec,
                  { kind: valTok.kind, value: valTok.value },
                  valTok.offset,
                );
                if (validated !== undefined)
                  tunables[spec.canonicalName] = validated;
              }
              positionalIndex++;
            }
          } else {
            // Unknown token
            const badTok = this.consume();
            this.errors.push({
              code: "resilience-malformed",
              message: `unexpected token '${badTok.value}' in argument list of '${policyName}'`,
              offset: badTok.offset,
            });
          }
        }

        if (this.peek().kind === "comma") {
          this.consume();
          // Trailing comma check
          if (this.peek().kind === "rparen") {
            this.errors.push({
              code: "resilience-malformed",
              message: `trailing comma in '${policyName}' argument list`,
              offset: this.peek().offset,
            });
            break;
          }
        } else {
          break;
        }
      }
    }

    const rparen = this.expect("rparen");
    if (!rparen) {
      this.errors.push({
        code: "resilience-malformed",
        message: `missing ')' for '${policyName}'`,
        offset: this.peek().offset,
      });
      return undefined;
    }

    if (this.errors.length > 0) return undefined;

    return {
      policy: policyName as "retry" | "circuitBreaker" | "singleflight",
      tunables,
      inner,
    };
  }

  /** Validate and normalize a raw token value against a tunable spec. */
  private validateTunableValue(
    policy: string,
    spec: TunableSpec,
    raw: RawArg,
    offset: number,
  ): number | boolean | undefined {
    const name = spec.canonicalName;

    if (spec.kind === "bool") {
      if (raw.kind !== "bool") {
        this.errors.push({
          code: "resilience-bad-arg",
          message: `'${policy}.${name}' is invalid: expected true or false, got '${raw.value}'`,
          offset,
        });
        return undefined;
      }
      return raw.value === "true";
    }

    if (spec.kind === "int") {
      if (raw.kind !== "number") {
        this.errors.push({
          code: "resilience-bad-arg",
          message: `'${policy}.${name}' is invalid: expected an integer, got '${raw.value}'`,
          offset,
        });
        return undefined;
      }
      const n = parseInt(raw.value, 10);
      if (spec.min !== undefined && n < spec.min) {
        this.errors.push({
          code: "resilience-bad-arg",
          message: `'${policy}.${name}' is invalid: value ${n} is below the minimum of ${spec.min}`,
          offset,
        });
        return undefined;
      }
      return n;
    }

    if (spec.kind === "duration-ms") {
      // The tokenizer only lexes positive integers and positive duration literals
      // (e.g. 200ms, 30s), so negative-value and NaN branches are unreachable
      // by construction — they are not present in this implementation.
      if (raw.kind === "number") return parseInt(raw.value, 10);
      if (raw.kind === "duration") return parseDurationToMs(raw.value);
      this.errors.push({
        code: "resilience-bad-arg",
        message:
          `'${policy}.${name}' is invalid: expected an integer (ms) or duration ` +
          `(e.g. 200ms, 30s), got '${raw.value}'`,
        offset,
      });
      return undefined;
    }

    // spec.kind === "duration-s" is the only remaining case (TunableKind exhausted above)
    if (raw.kind === "number") return parseInt(raw.value, 10);
    if (raw.kind === "duration") return parseDurationToSeconds(raw.value);
    this.errors.push({
      code: "resilience-bad-arg",
      message:
        `'${policy}.${name}' is invalid: expected an integer (seconds) or duration ` +
        `(e.g. 30s), got '${raw.value}'`,
      offset,
    });
    return undefined;
  }

  /** Skip tokens until we balance parens or reach eof — used for error recovery. */
  private skipArgList(): void {
    let depth = 1;
    while (this.peek().kind !== "eof") {
      const t = this.consume();
      if (t.kind === "lparen") depth++;
      else if (t.kind === "rparen") {
        depth--;
        if (depth === 0) {
          this.pos--; // put the rparen back
          return;
        }
      }
    }
  }
}

// ----------------------------------------------------------------
// Duration helpers
// ----------------------------------------------------------------

// Duration helpers — called only with tokens produced by the tokenizer, which
// guarantees the digit portion is non-empty and numeric; NaN/unknown-suffix
// branches are unreachable by construction.

function parseDurationToMs(dur: string): number {
  if (dur.endsWith("ms")) return parseInt(dur.slice(0, -2), 10);
  // dur.endsWith("s") — the tokenizer only produces "ms" or "s" suffixes
  return parseInt(dur.slice(0, -1), 10) * 1000;
}

function parseDurationToSeconds(dur: string): number {
  if (dur.endsWith("ms")) return parseInt(dur.slice(0, -2), 10) / 1000;
  // dur.endsWith("s") — the tokenizer only produces "ms" or "s" suffixes
  return parseInt(dur.slice(0, -1), 10);
}

// ----------------------------------------------------------------
// Public parse() entry point
// ----------------------------------------------------------------

/**
 * Parse a @d2Resilience pipeline-expression string into an AST.
 *
 * Returns `{ ok: true, root }` on success or `{ ok: false, errors }` on failure.
 * This function is pure — it has no side effects and does not import TypeSpec types.
 * The emitter re-parses the stored raw string to walk the AST.
 */
export function parse(expr: string): ResilienceParseResult {
  const trimmed = expr.trim();
  if (trimmed.length === 0) {
    return {
      ok: false,
      errors: [
        {
          code: "resilience-malformed",
          message: "expression must not be empty",
          offset: 0,
        },
      ],
    };
  }

  const tokens = tokenize(trimmed);
  if (tokens === null) {
    return {
      ok: false,
      errors: [
        {
          code: "resilience-malformed",
          message: "expression contains unrecognized characters",
          offset: 0,
        },
      ],
    };
  }

  const parser = new Parser(tokens);
  const root = parser.parse();

  if (parser.errors.length > 0) return { ok: false, errors: parser.errors };

  // parsePolicyCall always either returns a node or pushes at least one error;
  // because parser.errors.length === 0, root is always defined here.
  return { ok: true, root: root! };
}

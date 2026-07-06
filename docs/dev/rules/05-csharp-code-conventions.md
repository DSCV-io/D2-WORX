<!--
Copyright (c) DCSV. All rights reserved.
-->

## 5. C# Code Conventions
<a name="top"></a>
_[← rules index](../rules.md) · §5 of the D2-WORX rules catalog._

<!-- VERBATIM-BEGIN -->

The set of in-language rules that show up everywhere. Memory of these is the difference between first-pass clean and round-3 cleanup.

### Null / empty / parse helpers (highest-frequency)

- **5.1** Are all null / empty / whitespace / `Guid.Empty` checks using `Falsey()` / `Truthy()` extensions from `D2.Shared.Utilities.Extensions`?
  - **Forbidden**: `string.IsNullOrEmpty(s)`, `string.IsNullOrWhiteSpace(s)`, `coll is null || coll.Count == 0`, `coll?.Any() != true`, `guid == Guid.Empty`, `s != null && s != ""`.
  - **Required**: `s.Falsey()`, `coll.Falsey()`, `guid.Falsey()` (and `Truthy()` inverses) — they handle null themselves so never combine with `is null` checks. After early return on `Falsey()`, use `value!` (one of the few valid `!` uses).
  - **Also forbidden**: redundant size check after `.Falsey()` (`coll.Falsey()` already covers null + empty; a follow-up `coll.Count == 0` is dead code).
  - Evidence: `grep -rEn 'IsNullOrEmpty\|IsNullOrWhiteSpace\|== Guid\.Empty' <scope>` → expect zero (or justify each).
  - **Where defined**: `D2.Shared.Utilities.Extensions` — `StringExtensions.cs`, `GuidExtensions.cs`, `EnumerableExtensions.cs`.

- **5.1a** Do required-argument guards on string / collection / Guid values use `x.ThrowIfFalsey()` instead of raw BCL guards or hand-rolled throws?
  - **Forbidden** (for string / collection / Guid args): `ArgumentException.ThrowIfNullOrWhiteSpace(s)`; `ArgumentNullException.ThrowIfNull(coll)` paired with a manual empty-check; hand-rolled `if (s.Falsey()) throw new ArgumentException(...)`.
  - **Required**: `s.ThrowIfFalsey()` / `coll.ThrowIfFalsey()` / `guid.ThrowIfFalsey()` — BCL-split (`ArgumentNullException` for literal null; `ArgumentException` for present-but-falsey: empty/whitespace string, empty collection, `Guid.Empty`); `[CallerArgumentExpression]` auto-captures the parameter name (pass an explicit `paramName` only for indexed or computed sites such as `additionalScopes[i]`).
  - **Carve-outs** — at each carve-out site, add a one-line comment citing this predicate:
    - **Plain reference-type null-guards**: DI services / loggers / options where there is no present-but-falsey concept — use BCL `ThrowIfNull`.
    - **Generated files + codegen emitter-output strings** (§26): never hand-edit generated output.
    - **Projects that do not reference `D2.Shared.Utilities` due to a GENUINE DEPENDENCY CYCLE**: e.g. the `I18n.Abstractions ← Utilities` dependency cycle — adding the reference would introduce a cycle. This carve-out is for genuine cycles ONLY. Do NOT decline a `D2.Shared.Utilities` reference for "purity" / "minimal-deps aesthetics" when no cycle exists — if the project CAN reference Utilities without a cycle, it MUST, and hand-rolled `== Guid.Empty` / `IsNullOrEmpty` / `Count == 0` guards there are a §5.1 / §5.1a violation.
    - **Bespoke-message guards**: `ThrowIfFalsey` has no custom-message overload; when the exception message must carry domain-specific guidance (e.g. the `ForScopes` "use `HarmlessEndpoint`" hint), keep the explicit `throw new ArgumentException(...)` and comment the carve-out.
  - **Where defined**: `D2.Shared.Utilities.Extensions.GuardExtensions`.
  - **Why**: extends §5.1's Falsey/Truthy unification to guard clauses — one call covers null + empty + whitespace + empty-collection + `Guid.Empty` with the idiomatic BCL exception split, eliminating fragmented two-step guards.
  - Evidence: `grep -rEn 'ArgumentException\.ThrowIfNullOrWhiteSpace\|ArgumentNullException\.ThrowIfNull' <scope>` → per hit, confirm carve-out applies or convert.

- **5.2** Are all `TryParse` patterns using `D2.Shared.Utilities.Extensions` (`str.TryParseTruthyNull(out Guid? r)` / `str.TryParseTruthyNull<TEnum>(out var r)`)?
  - **Forbidden**: hand-rolled `if (str is not null && Guid.TryParse(...))` / `Enum.TryParse<T>(...)`.
  - **Required**: the extension that collapses null/empty/whitespace/Guid.Empty/unparseable → `null` in one call.
  - Evidence: `grep -rEn 'Guid\.TryParse\|Enum\.TryParse' <scope>` → for each, justify or convert.
  - **Applies**: hand-written code AND codegen emitter output. When MutableEmitter / TKEmitter / ScopesEmitter generate code that touches strings or Guids, the emission should produce code that calls these extensions.

- **5.3** Are all `D2Result` failure constructions using semantic factories (`Ok`, `Created`, `NotFound`, `Unauthorized`, `Forbidden`, `ValidationFailed`, `Conflict`, `ServiceUnavailable`, `UnhandledException`, `PayloadTooLarge`, `Canceled`, `SomeFound`)?
  - **Forbidden**: raw `Fail()` with manual `statusCode` when a factory exists.
  - **Allowed**: raw `Fail` ONLY when no factory matches (e.g. re-mapping arbitrary upstream status codes).
  - Evidence: `grep -rEn '\.Fail\(' <scope>` → per hit, justify or convert.
  - **Partial-success pattern**: `NOT_FOUND` (none found) → `SOME_FOUND` (partial, data returned) → `OK` (all found).
  - **If a typed/generic semantic factory is missing** that should exist (e.g. `D2Result<T>.ServiceUnavailable()`): that's a bug in `D2.Shared.Result`, not a justification for raw `Fail`. Add the factory.

- **5.4** Does code at boundaries (proto/DB/external) use `.ToNullIfEmpty()` instead of letting `""` survive into domain types?
  - Evidence: per boundary → `ToNullIfEmpty` confirmed.
  - **Returns**: `null` if the string is null, empty, or whitespace-only (trims first).

- **5.5** Is `string.Empty` used everywhere instead of `""` (StyleCop SA1122)?
  - Evidence: build clean confirms.

### Syntax and structure

- **5.6** Are extension methods using C# 14 extension-members syntax (`extension(T target) { ... }`) instead of the old `this T` parameter style?
  - **Carve-out**: extension methods on nested generic async receivers (e.g. `ValueTask<D2Result<T>>`) and fluent async chains may use the old `this T target` parameter style when the C# 14 `extension(T target) { ... }` block form fails to resolve (CS1061 on the nested-generic receiver) or triggers spurious CA2012 "ValueTask awaited multiple times" warnings on fluent intermediates. Document the carve-out with a one-line comment at the file head citing this predicate — this is a permitted exception to §7.16's "default to no comments" rule because the compiler-bug rationale is exactly the "WHY non-obvious" §7.16 carve-out criterion. Revisit when C# 15+ resolves the underlying compiler limitations OR when the fluent extension pattern is refactored. Reference instance: `server/shared/dotnet/result/core/D2ResultAsyncExtensions.cs`.
  - **Carve-out application criterion** (mechanical, not "I tried it once"): the carve-out applies ONLY when BOTH (a) the C# 14 block form was attempted in the same file AND (b) the build surfaced CS1061 OR CA2012 specifically against the block-form extension's receiver. Generic CS1061s caused by unrelated bugs (missing using, wrong receiver type, typo) do NOT trigger the carve-out. The comment cites the specific error code seen.
  - Evidence: per new extension → syntax confirmed, or carve-out documented at file head with reference to this predicate + the specific compiler error code that justified the carve-out.

- **5.7** Are all concrete classes / records / exceptions / attributes marked `sealed`?
  - **Carve-outs**: (1) types that are explicit base classes for other types in the codebase (e.g. `D2Result` stays unsealed because `D2Result<TData>` derives from it; xUnit collection-fixture bases qualify under this carve-out); (2) static classes (already implicitly sealed). Test classes are otherwise sealed (xUnit instantiates reflectively but does not subclass).
  - **Why**: enables JIT devirtualization on virtual / interface call sites and signals "this is not an extension point." Unsealing later is cheap; sealing retroactively is not.
  - Evidence: per new concrete type → `sealed` confirmed or carve-out documented.

- **5.8** Are single-line `if`/`while`/`for`/`foreach` bodies WITHOUT braces, and multi-line bodies WITH braces?
  - **Rule**: visually multi-line bodies (body wraps onto multiple lines because the body itself wraps, or a constructor / method call breaks across lines) ALWAYS get braces, regardless of how many statements they logically contain.
  - **Why**: a multi-line body without braces lets the next sibling statement visually merge with the body — the C dangling-`else` footgun, real source of bugs when refactoring.
  - **Two acceptable brace-less forms — use one or the other, no in-between**:
    - **Form A — single line**: `if (cond) return foo;` — entire if + body on one source line. No padding required (this is the standard guard-clause pattern at the top of methods).
    - **Form B — two-liner with `if` on its own line**: permitted ONLY with blank lines BOTH above AND below the if-block. The brace-less body needs visual breathing room; without padding, the body reads as a continuation of the surrounding sequential code.
    - Three-or-more-line bodies (or wrapped/multi-line bodies) MUST have braces — SA1519 already enforces this.
  - Evidence: spot-check new control flow; `grep -rEn '^[[:space:]]+if[[:space:]]\([^)]+\)[[:space:]]*$' <scope>` finds Form-B candidates whose padding then needs visual verification.

- **5.8a** Does every C# method body place a single blank line **before AND after** every control-flow block and every multi-line statement?
  - **Scope**: applies to hand-authored `.cs` files. Does NOT apply to generated files (`*.g.cs`, files under `Generated/`) or EF migration files.
  - **Required blank-line padding on both sides for**:
    - Every `if` / `else if` / `else` / `while` / `for` / `foreach` / `switch` block and `using`-statement.
    - Every **multi-line statement** — a single statement spanning ≥2 physical lines (a wrapped extension-method chain, a multi-line method call, a multi-line `return` / object-initializer used as a statement).
  - **Consecutive simple single-line statements** (sequential assignments, simple `return`s, simple method calls that each fit on one line) do NOT require padding between them — group them naturally.
  - **StyleCop interaction** (never violate these constraints while adding padding):
    - SA1505 — no blank line after an opening brace.
    - SA1507 — no double blank lines anywhere (single blank line ONLY).
    - SA1508 — no blank line before a closing brace.
    - SA1510 — no blank line immediately BEFORE a `catch` / `finally` / `else` / `else if` / `do…while`-`while` continuation keyword. The "blank line before every control-flow block" requirement does NOT extend to these continuation keywords: a `catch` is part of the same `try` statement as the block above it, an `else` is part of the same `if`, and the `while` of a `do…while` closes the loop above it. Mechanically padding before `catch` / `else` to satisfy the "blank line before every block" instinct trips SA1510. Padding still applies before the OPENING `try` / `if` / `do` and after the WHOLE statement closes — just never between a block and its own continuation keyword.
    - SA1516 — member separator (blank line between type members) is pre-existing; the per-method body padding adds blank lines WITHIN a method body and does not conflict with SA1516.
  - **Cross-ref**: §5.8 (brace rules) is the sibling predicate. §5.8 Form-B already mandates blank lines above AND below a brace-less two-liner `if` — §5.8a generalizes that requirement to every control-flow block and every multi-line statement inside a method body.
  - **Why**: control-flow blocks and wrapped statements create visual noise when packed against surrounding sequential code. Blank lines on both sides make the boundary between "decision / complex expression" and "simple sequential flow" scannable at a glance — the eye can parse the method in one pass without counting keywords. The pattern also naturally exposes when a method does too much (dense padding is a signal to extract).
  - **How** — gold-standard example (from KeyCustodian `CompromiseKeyHandler`, adopted 2026-06-18, deliverable 0022):
    ```csharp
    var kidResult = Kid.Create(input.Kid);

    if (kidResult.BubbleOnFailure<Kid, CompromiseKeyOutput>(out var bubbled, out var kid))
        return bubbled;

    var record = await db.Keys
        .Live()
        .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
        .ConfigureAwait(false);

    if (record is null)
        return KeyCustodianFailures<CompromiseKeyOutput?>.KeyNotFound();
    ```
    Note: the `await db.Keys…` chain is a multi-line statement → blank line before AND after. Each `if` block → blank line before AND after. Simple single-liners (`var kidResult = …`) do NOT get padding against each other.
  - Evidence: read each new or modified `.cs` file in scope (not grep — tool-invisible); confirm every `if`/loop/switch/`using`-statement and every wrapped statement has a blank line on both sides; no doubles (SA1507), no blanks at block start/end (SA1505/SA1508).
  - *Provenance: deliverable 0022 (the blank-line readability convention surfaced from the post-step user review; the SA1510 caveat from the S4 audit catching catch-after-try padding).*

- **5.9** Is `this.` qualifier absent? (Codebase doesn't use it; field prefixes already disambiguate.)
  - Evidence: `grep -rEn 'this\.' <scope C# files>` → expect zero in introduced code.

- **5.10** Is `namespace` declared BEFORE `using` directives in every `.cs` file? (Codebase convention: file-scoped `namespace X;` on line N, blank line, then `using` block.)
  - Evidence: per new `.cs` file → ordering confirmed.

- **5.29** Does every concrete handler implementation file (a non-abstract `<Op>Handler` deriving `BaseHandler<…>` / `BaseRepoHandler<…>`) declare the three file-scoped `using` aliases `H` / `I` / `O` and use them in all five canonical slots?
  - **The triad** (file-scoped `using` directives, placed after the file-scoped `namespace …;` per §5.10):
    - `using H = <FQN>.I<Op>Handler;` — the handler's OWN interface (the `H` of "handler").
    - `using I = <input-type-FQN>;` — the operation's input type.
    - `using O = <output-type-FQN>;` — the operation's output type.
  - **The five slots that MUST use the aliases**: (1) `BaseHandler<TSelf, I, O>` / `BaseRepoHandler<TSelf, I, O>` type args; (2) the implemented-interface position — `… , H`; (3) the `ExecuteAsync(I input, CancellationToken ct)` parameter; (4) the `ValueTask<D2Result<O?>>` return; (5) any `D2Result<O?>` constructed in the body for the op's own output (`new O(...)`, `D2Result<O?>.Ok(...)`).
  - **TSelf is NEVER aliased**: the handler's own class name stays spelled out in the `BaseHandler<TSelf, …>` first slot (it names the type for OTel span naming). There is no `S`/`Self` alias.
  - **`I` here is the INPUT-TYPE alias — NOT the `I`-prefix interface-naming rule.** Do not flag `using I = <Op>Input;` as an interface-naming convention violation (§7) — `I` is a deliberate one-letter alias for the input type, mirroring `O` for output and `H` for handler. (Auditor false-positive guard.)
  - **Domain types in the body are NOT aliased** — only the op's input/output get `I`/`O`. Domain aggregates / VOs / nested projections (`PendingKey`, `ActiveKey`, `Kid`, nested DTOs) keep their real names.
  - **Aliases are PER-FILE, never global** — each op needs a different `H`/`I`/`O`, so globalizing them would collide. Optional convenience aliases for frequently-referenced dependency namespaces (a `ReadRepo = …` / `CreateRepo = …`) are permitted but not part of the mandatory triad.
  - **Scope / carve-out**: applies to concrete handler implementation files. The interface file `I<Op>Handler.cs` does NOT take the triad. A module-within-host's handlers (KeyCustodian, the auth module) are in scope.
  - **Why**: a handler's type args, implemented interface, and `ExecuteAsync` signature repeat the input/output/interface FQNs up to five times; for ops whose I/O are generated transport DTOs the fully-qualified names dominate the declaration. The three-letter triad makes the signature scannable — the file reads as "this is the H for I→O." Established v1 shape reinstated.
  - **Evidence**: per concrete handler file → confirm the three aliases are present AND used in all five slots; confirm TSelf is spelled out (not aliased); confirm the aliases are file-scoped (absent from `GlobalUsings.cs`). A handler spelling out the FQNs instead of the aliases = FINDING. A handler missing one alias, or aliasing TSelf, = FINDING.
  - **How**: place the file-scoped `namespace …;` first (§5.10), then the using block ending with the three aliases; declare `public sealed class <Op>Handler(…) : BaseHandler<<Op>Handler, I, O>(ctx), H`; write `ExecuteAsync(I input, CancellationToken ct)` returning `ValueTask<D2Result<O?>>`. Reference: every `server/services/edge/key-custodian/app/Application/Handlers/**/<Op>Handler.cs`.
  - *Provenance: deliverable 0026 sub-step 4a — the v1 `H`/`I`/`O` alias convention reinstated + retrofit across the 11 KeyCustodian handlers.*

### Records, collections, options patterns

- **5.11** Are entities using `record` types with `required init` properties + empty collection initializers (`[]`)?
  - Evidence: per new entity → record + `required init` + collection-expression defaults.

- **5.12** Are collection expressions (`[a, b, c]` / `[]`) used instead of `new T { ... }` / `new[] { ... }` / `Array.Empty<T>()`?
  - **Required when** target type is `IEnumerable<T>` / `IReadOnlyList<T>` / `IList<T>` / `T[]` / `Span<T>` (or any constructible collection). Compiler picks the best concrete type for the slot (often a stack-allocated span or a single allocation) and call sites read at half the noise level.
  - **Allowed**: explicit `new List<T>()` / `new T[N]` when you need that exact concrete type, a specific capacity hint, or a mutable list reference.
  - **Hot-spots**: defaults for `IReadOnlyList<T>` parameters, fallback values in ternaries (`x ? values : [defaultValue]`), single-item array args.
  - Evidence: per collection literal → expression form.

- **5.13** Do small Options records (≤4 properties) use the nullable-param ctor + `?? default` body pattern? Pattern at canonical `D2.Shared.Resilience.CircuitBreaker.CircuitBreakerOptions`.
  - **Shape**: parameterized ctor with EVERY param nullable + `?? default` body assignment, plus a parameterless ctor that chains `: this(null, null, ...)` to inherit defaults. Yields `new(failureThreshold: 3)` / `new()` / `new(3, TimeSpan.FromSeconds(5))` call sites.
  - **For 5+ properties**: stay on init-only properties + object initializer (positional ordering becomes hard to read).
  - **Sentinel-free**: explicit non-null values (including `0` / `TimeSpan.Zero`) pass through unchanged.
  - Evidence: per new Options record → pattern confirmed.

- **5.14** Are nullable types used for optional domain fields (`string?`, `bool?`, `int?`, `DateTime?`)? Never `= string.Empty` on optional record properties. `null` = "not provided."
  - Evidence: per optional field → `?` form + no empty-string default.

### Async / threading / asynchrony

- **5.15** `ValueTask` single-await discipline. See §4.6 for the canonical predicate (concurrency category is the natural home). This row is the §5 cross-pointer; walk §4.6 for evidence.

- **5.16** Are async methods consistently named with the `Async` suffix?
  - Evidence: per async method → suffix confirmed.

- **5.17** Are `await`-ed calls passed a `CancellationToken` whenever the API supports it?
  - Evidence: per `await` of a ct-accepting API → ct passed.

### Public-API surface

- **5.18** Do all `public` types / methods have XML doc comments?
  - **Format**: `<summary>`, `<param>`, `<returns>`, `<exception>` as appropriate. Wrap onto multiple lines if needed for line-length compliance.
  - **Synchronously-invoked callback parameters** (`onX`, `onSomething`, `Action<>` / `Func<>` ctor args invoked inside `lock` / on the calling thread) MUST document throw-behavior in their XML `<param>` block — specifically, what happens to the upstream exception if the callback throws. Default platform behavior is "the thrown exception REPLACES the upstream exception that triggered the invocation" — a buggy logger inside the callback can silently swap a meaningful "TimeoutException from upstream X" for "InvalidOperationException from logger" and make outage diagnosis painful. Document loudly so callers wrap in their own try/catch (or stick to log/metric calls that won't throw).
  - Evidence: per new public symbol → `<summary>` confirmed; per callback param → throw-behavior `<para>` block confirmed.

- **5.19** Does each handler implement its declared interface (for DI registration)?
  - Evidence: per new handler → interface implementation confirmed.

### Regex (ReDoS discipline)

- **5.20** Do `[GeneratedRegex]` patterns classify into the right backtracking bucket and apply timeout discipline accordingly?
  - **Bucket 1 — no backtracking → NO timeout.** Single greedy quantifier with no following pattern (`\s+`); single char-class match with no quantifier (`[^\d]`, `[^\p{L}\p{N}\s\-'.,]`); quantifier whose char class is disjoint from the next required token (`\w+\}` — `\w` can't match `}`).
  - **Bucket 2 — linear backtracking AND input upstream-bounded → NO timeout.** Greedy quantifier followed by an overlapping required token, but each backtrack attempt is O(1) and total attempts grow at most linearly with input length. Example: `[^@\s]+\.[^@\s]+`. Document the linear-time guarantee + the input-length assumption in the pattern's XML doc so future-you can audit when adding new call sites.
  - **Bucket 3 — super-linear backtracking → set tight `matchTimeoutMilliseconds` (10–25 ms) AND pre-warm the JIT.** Nested quantifiers (`(a+)+`, `(a*)*`); alternation with overlap (`(a|aa)+`); polynomial / exponential backtracking. Pre-warm via `static readonly bool sr_jitWarmedUp = WarmUpHelper();` field initializer so first user-visible call doesn't pay JIT cost inside the timeout window.
  - **Document the bucket** in the pattern's `<summary>`. A pattern change that promotes Bucket-1/2 → Bucket-3 needs a timeout + pre-warm added in the same edit.
  - **Why**: ReDoS attacks rely on super-linear backtracking. A _tight_ timeout on linear patterns occasionally fails under GC pauses / scheduling jitter even on sub-microsecond matches.
  - Evidence: per new `[GeneratedRegex]` → bucket classification + matching timeout discipline.

### Build cleanliness (zero tolerance)

- **5.21** Does `dotnet build server/D2.slnx` produce zero StyleCop (SA\***\*), CS\*\*** warnings, null ref warnings? Never suppress with `#pragma warning disable`, `!` (for silencing warnings), or analogous.
  - **Container coordination (cross-ref §8.2)**: host `dotnet build server/D2.slnx` collides with `dotnet watch` containers via the shared `obj/` mount and crashes geo/gateway/signalr. Before running this gate while Compose is up, EITHER stop the active .NET containers (`docker compose stop <containers>`) OR run the build INSIDE a container. Re-start containers after the gate passes.
  - Evidence: build output, plus `docker compose ps` snapshot showing .NET containers stopped (or build-in-container trace) when Compose was active.

- **5.22** Does `jb inspectcode server/D2.slnx --severity=WARNING` produce zero JetBrains/Rider warnings? (Catches `[MustDisposeResource]` misuse, captured variable/closure issues, `AccessToModifiedClosure`, `AccessToDisposedClosure` — invisible to `dotnet build`.)
  - Evidence: inspectcode output.

- **5.23** Are ALL warnings/errors encountered ANYWHERE in the project fixed (zero-tolerance)? Never dismiss as "pre-existing."
  - Evidence: `git diff main` cross-check confirms no leftover warnings.

- **5.24** Foundational shared libs (the lib that DEFINES a convention) MUST eat their own dogfood. The lib that exports `Falsey()` cannot use `string.IsNullOrEmpty` internally; the lib that exports `TryParseTruthyNull` cannot hand-roll `Guid.TryParse` + null check; the lib that exports the `[RedactData]` attribute cannot log raw user input. Foundation libs are the strictest dogfood site in the codebase — any lib that publishes a "use this not that" helper must be the canonical demonstration of using it.
  - Evidence: when auditing a foundation lib, grep its OWN source for the forbidden patterns the convention prohibits; expect zero hits.

- **5.25** Does production code that emits codegen'd member names (Serilog diagnostic-property keys, OTel span-tag keys, OTel metric-tag keys, JSON field names that mirror an interface property, telemetry counter labels, AMQP header names that mirror a domain property) use `nameof(SourceOfTruthType.Member)` rather than raw string literals?
  - **EXEMPTION**: spec-pinning tests that explicitly assert "this exact name exists on the wire" KEEP literal strings — the literal IS the pin. Same exemption applies to constants whose literal value IS the wire format (e.g. JWT claim-type constants, OAuth scope constants, AMQP exchange names) — those are spec-anchored and should never be `nameof`-derived.
  - Evidence: `grep -rEn 'diagnosticContext\.Set\("|Activity\.SetTag\("|AddTag\("|new TagList \{ \{ "' <production scope>` returns zero raw-literal hits where a `nameof(IInterface.Member)` form would compile. Test files that intentionally pin literal wire values are exempted in the per-row N/A reason.
  - **Why**: raw literals defeat compile-time rename safety. When the source-of-truth member is renamed (e.g. `IRequestContext.SessionId` → `IRequestContext.UserSessionId`), every raw literal `"SessionId"` in production emission code silently drifts to the WRONG wire value while still compiling. Loki / Tempo / Elasticsearch queries that filtered on `SessionId` continue to work for old log lines; new log lines emit the new name; operators see a partial-data outage with no compile-time signal. `nameof(IRequestContext.UserSessionId)` makes the rename surface as a build break in every emission site, forcing an explicit migration decision.
  - **How**: when emitting a structured-log property, span tag, metric tag, JSON field, or any other wire-format key that mirrors a domain interface member, use `nameof(IInterface.Member)` not the raw string literal. Spec-pinning tests that assert "the literal `\"SessionId\"` appears in the rendered output" stay literal (the pin is the entire point). When in doubt, the rename test is: "if I rename the source-of-truth member tomorrow, do I want this site to break the build?" If yes → `nameof`. If no → literal is correct.

- **5.25a** After a `.Should().NotBeNull()` assertion (AwesomeAssertions), is the null-forgiving operator `!` absent from any immediately following member access on the same variable?
  - **Required**: use `x.Member` — NOT `x!.Member`. After `.Should().NotBeNull()`, AwesomeAssertions flows a non-null post-condition that the C# compiler AND `jb inspectcode` both recognize. The `!` operator is therefore redundant, and `jb inspectcode` flags it as unnecessary.
  - **Why**: matches the §5.21 / §5.22 zero-warnings-both-tools mandate. Redundant `!` operators specifically appear in sub-agent-written test code after `NotBeNull()` assertions; this predicate makes the violation explicitly auditable.
  - **False-positive carve-out — AwesomeAssertions 9.x does NOT ship `[NotNull]`**: AwesomeAssertions' `.Should().NotBeNull()` returns `AndConstraint<T>`, not the original value, and the method carries no `[NotNull]` post-condition attribute. As a result, C# nullable flow analysis does NOT infer non-null on the original variable after the call — the compiler still considers `x` nullable, and `x!.Member` on the next line is COMPILER-REQUIRED, not redundant. (By contrast, FluentAssertions 6+ ships `[NotNull]` on `NotBeNull()`, making the `!` there genuinely redundant.) When the assertion library is AwesomeAssertions, `x!.Member` following `.Should().NotBeNull()` is NOT a §5.25a violation — flag it N/A in the audit row with this rationale, and do not dispatch a Fixer to remove the `!`.
  - Evidence: `grep -rEn '\.Should\(\)\.NotBeNull\(\)' <test scope>` → per hit, confirm the following access on the same variable does NOT carry `!`; if it does, check whether the assertion library is AwesomeAssertions (carve-out applies) or FluentAssertions (violation).

### Global usings (the two-tier frequency-driven policy)

- **5.26** Does the global-usings set follow the two-tier frequency-driven policy — no duplicates of SDK ImplicitUsings or Tier-1 entries, no unused globals flagged by inspectcode? (Canonical: [ADR-0020](../adrs/0020-service-project-structure.md) "the global-usings policy".)
  - **Tier-1 — service-project scope** (central `<Using>` items in `server/services/Directory.Build.targets`): `D2.Shared.Result` · `D2.Shared.Utilities.Extensions` + `.Attributes` + `.Enums` · `D2.Shared.I18n`. Service projects (`server/services/`) reliably reference the full D2 runtime stack; the `netstandard2.0` condition in the targets file excludes source-gen shells. **Shared libs (`server/shared/dotnet/`) are excluded** — `D2.Shared.I18n` is split across three assemblies (`I18n.Abstractions`, `I18n.Keys`, `I18n` core) and the Tier-1 libs form a dependency chain; a blanket runtime-wide global produces hard CS0246 errors in libs that sit below the full stack. Shared libs keep explicit usings.
  - **Tier-2 — per project** (`GlobalUsings.cs` per project, frequency-driven): a project MAY globalize ANY namespace it legitimately references — including `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `System.Security.Cryptography`, or any vendor SDK — when that namespace is repeated across roughly ≥3 files in that project. The dependency law is enforced by `<ProjectReference>` edges in the csproj (compile-time), not by per-file using visibility; a global using is per-project and cannot leak the namespace across the layer boundary. Per-file usings remain for low-frequency (1–2 file) namespaces. Reference implementation: KeyCustodian `domain/GlobalUsings.cs` + `app/GlobalUsings.cs` + Edge `tests/GlobalUsings.cs`. (`NodaTime` is Tier-2 not Tier-1 because it is the domain's #1 import but absent from pure-DTO projects — central globalization would force a NodaTime package ref into projects that never use it.) **SA1200 exemption**: `GlobalUsings.cs` files are exempt from SA1200 (the inside-namespace placement rule) via a `[**/GlobalUsings.cs]` section in `.editorconfig` — `global using` directives are top-level by C# language rule and cannot be nested inside a namespace declaration.
  - **Global aliases are permitted** — the established `global using IClock = D2.Shared.Time.IClock;` alias in every project that uses both NodaTime and `D2.Shared.Time` resolves the `NodaTime.IClock` vs `D2.Shared.Time.IClock` CS0104 ambiguity project-wide, eliminating per-file alias repetition.
  - **Hard constraints** (regardless of frequency): (a) never duplicate a namespace already covered by SDK ImplicitUsings (`System`, `System.Collections.Generic`, `System.Threading`, `System.Threading.Tasks`, etc.); (b) never duplicate a Tier-1 entry; (c) never globalize in a shared lib (`server/shared/dotnet/`) — shared libs keep explicit usings.
  - **Why**: the dependency law is enforced at the `<ProjectReference>` boundary, not at using-directive visibility. A domain project that does not reference `Microsoft.EntityFrameworkCore.dll` cannot compile any EF type regardless of whether a global using appears in the project — the csproj reference graph is the compiler-enforced layer boundary. Frequency-driven globals eliminate the highest-repetition usings with no loss of signal; low-frequency usings stay explicit as a natural readability cue.
  - **Cleanup that rides along**: keep `<ImplicitUsings>enable` (already on); delete redundant central `<Using>` items that the ImplicitUsings default set already covers (`System`, `System.Collections.Generic`, `System.Threading`, `System.Threading.Tasks`).
  - Evidence: per `GlobalUsings.cs` → (a) compare each global entry against the SDK ImplicitUsings default set + Tier-1 entries — any overlap = FINDING (duplicate); (b) run `jb inspectcode` → zero "using directive is not required" warnings on `GlobalUsings.cs` files (an entry that is never resolved in the project = unused global = FINDING); (c) per project reference graph → confirm `<ProjectReference>` edges respect the one-direction dependency law (§9.36) — a global using in a domain project for `Microsoft.EntityFrameworkCore` with no EF csproj reference is a csproj violation, not a global-using violation, and is caught by §9.36 evidence. Cross-ref §9.36 (dependency-direction law).

- **5.27** Is `[Required]` NEVER used on a non-nullable struct property (it is a no-op), with a real range/custom validator used instead?
  - **Forbidden**: `[Required]` on a value-type property (`[Required] public TimeSpan Cadence`, `[Required] public int Size`) — a value type is never "missing," so `ValidateDataAnnotations` never fires; the annotation LOOKS like validation but validates nothing.
  - **Required**: a real range validator (`[Range(typeof(TimeSpan), "00:00:01", "365.00:00:00")]`) or a custom `.Validate(o => …, "…")` predicate on the options pipeline, AND the domain VO's smart constructor as the second floor (it rejects the invalid value on every path — config, future API, test).
  - **Why**: belt-and-suspenders — the options validator catches config errors at boot (an operator typo'd a `0` cadence); the domain VO catches every path. `[Required]` on a struct is the trap that passes review because it *reads* as validation. Empirical origin: an as-built `RotationPolicyOptions` carried `[Required] public TimeSpan Cadence` — a silent no-op.
  - Evidence: `grep -rEn '\[Required\]' <options scope>` → per hit, confirm the decorated property is a reference type (nullable struct or class); a non-nullable struct property carrying `[Required]` = FINDING (replace with `[Range(typeof(…),…)]` / custom validator). Cross-ref §23 (configuration hygiene), [ADR-0020](../adrs/0020-service-project-structure.md) (options pipeline).

- **5.28** (Prettier-clean commits — CODE files; Markdown is EXCLUDED) Are all of a commit's touched Prettier-handled CODE files Prettier-clean? The Prettier scope is `.ts` / `.js` / `.mjs` / `.cjs` / `.json` / `.svelte` / `.html` / `.css` / `.yaml` / `.yml`. The `.husky/pre-commit` hook enforces it mechanically — it runs `prettier --check` on the staged subset of that glob and BLOCKS a commit whose staged code files are not clean.
  - **Required**: before committing, the touched code files pass `prettier --check` (respecting `.prettierignore`). Run `pnpm format` (write) or `pnpm format:check` first. The pre-commit hook is the mechanical backstop; never `--no-verify` past it to land a dirty code file. Generated output (`**/*.g.ts`, `Generated/`, the machine-emitted fixtures) is `.prettierignore`d — formatting is the emitter's responsibility, not Prettier's.
  - **Markdown carve-out — NEVER Prettier a `.md` file**: `**/*.md` is in `.prettierignore`, and `md` is dropped from the `format` / `format:check` globs + the hook glob. Prettier's Markdown emphasis-normalization (`*` ↔ `_`) cannot distinguish an emphasis marker from an underscore inside an identifier or an asterisk in a glob, so on `--write` it CORRUPTS this identifier-heavy docs tree — `SCREAMING_SNAKE` → `SCREAMING*SNAKE`, `*.json` → `_.json`, `PHASE_3.md` → `PHASE*3.md` — and `--check` then calls the garbled output "clean" because it is stable. There is no Prettier option to disable emphasis-normalization. Markdown is governed by the documentation conventions in §11, NOT by Prettier.
  - **Why**: a clean code-formatting baseline keeps diffs reviewable and commits free of whitespace churn; the mechanical hook prevents the slow re-accumulation of drift. The Markdown carve-out exists because a repo-wide `prettier --write` on the docs silently mangled ~121 identifier-heavy Markdown files before it was caught — the corruption passes `--check`, so it is invisible to the gate and only a content read surfaces it.
  - **How**: code files → `pnpm format` + the hook. Markdown → the doc conventions (§11) + manual editing; if `prettier` is ever about to touch a `.md`, stop — the ignore + globs should already prevent it.
  - **Evidence**: `pnpm format:check` → exit 0 (zero dirty code files); `.prettierignore` contains `**/*.md`; the `format` / `format:check` / hook globs do NOT contain `md`; `.husky/pre-commit` is executable and blocks a dirty staged code file. Empirical origin: the repo-wide Markdown corruption recovery that motivated this carve-out.

- **5.30** Are local variable declarations using `var` wherever the type is evident from the right-hand side, rather than an explicit type?
  - **Scope**: LOCALS ONLY. Never `var` on fields, properties, or parameters — those keep their explicit types.
  - **Required**: `var` for a local whose type the compiler infers from the initializer (`var start = i + 1;`, `var record = await db.Keys.FirstOrDefaultAsync(...);`). Convert explicit-type locals — `int start = i + 1;` → `var start = i + 1;`.
  - **Carve-outs** (keep the explicit type):
    - `const` locals — a `const` cannot be `var`.
    - A local whose written type is LOAD-BEARING — e.g. an interface-typed local declared to pin the static type or steer overload resolution (`IEnumerable<T> items = concreteList;`). Switching to `var` would change the compile-time type / the overload chosen, so it stays explicit.
    - Initializers where `var` will not compile — `var x = null;`, or any ambiguous / untyped right-hand side.
  - **Why**: `var` trims declaration ceremony and keeps the eye on the value; the type is already on the right-hand side. The load-bearing carve-out preserves the cases where the written type actually changes behavior.
  - **Gate note**: `jb inspectcode` does NOT flag an explicit-type local where `var` would compile — this never surfaces at the build / inspect gate, so it is enforced by convention (Implementer / Fixer / Auditor briefs) alongside the rest of §5–§7.
  - Evidence: read new / modified `.cs` locals in scope (not a grep target — tool-invisible); per explicit-type local, confirm a carve-out applies (`const`, load-bearing type, non-inferable initializer), else convert to `var`.

<sup>[↑ jump to top](#top)</sup>

---


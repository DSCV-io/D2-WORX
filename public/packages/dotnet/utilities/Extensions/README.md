<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities — Extensions

> Part of [`DcsvIo.D2.Utilities`](../README.md).

The most-used surface in the lib. Boundary-check helpers (`Truthy`/`Falsey`/`ToNullIfEmpty`), optional-string parsers (`TryParseTruthyNull`), display cleaners, and `D2Result`-returning validators that compose into smart-constructor patterns.

| File                                                     | Contents                                                                                                                                                                                                                                                                           |
| -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `StringExtensions.cs`                                    | `Truthy()` / `Falsey()` / `ToNullIfEmpty()` / `CleanStr()` / `CleanDisplayStr()` / `TryParseEmail()` / `TryParsePhoneNumber()` / `GetNormalizedStrForHashing()` / `NormalizeForHash()`.                                                                                             |
| `EnumerableExtensions.cs`                                | `Truthy()` / `Falsey()` for `IEnumerable<T>?` + the `Clean()` helper with configurable empty/null behavior.                                                                                                                                                                        |
| `CleanEnumEmptyBehavior.cs`, `CleanValueNullBehavior.cs` | Behavior enums for `EnumerableExtensions.Clean()`.                                                                                                                                                                                                                                 |
| `GuidExtensions.cs`                                      | `Truthy()` / `Falsey()` for `Guid` and `Guid?` (treats `Guid.Empty` as falsey) PLUS `string?.TryParseTruthyNull(out Guid?)` — the canonical "parse a Guid from optional string input, collapse missing/unparseable/empty to null" helper.                                          |
| `EnumExtensions.cs`                                      | `string?.TryParseTruthyNull<TEnum>(out TEnum?)` — case-insensitive `Enum.TryParse` wrapper that collapses missing/unparseable/empty to `null`; pass-through on numeric strings (matches BCL behavior — does NOT call `Enum.IsDefined`); supports comma-separated `[Flags]` syntax. |
| `GuardExtensions.cs`                                     | `ThrowIfFalsey()` for `string?` / `IEnumerable<T>?` / `Guid?` / `Guid` — required-argument guards with BCL-split exceptions (`ArgumentNullException` for literal null, `ArgumentException` for present-but-falsey). `[CallerArgumentExpression]` auto-captures the parameter name. |

## Required-argument guards — `ThrowIfFalsey()`

The canonical way to guard required string / collection / Guid parameters. Extends the `Falsey()` / `Truthy()` convention to guard clauses — one call covers null + empty/whitespace + empty-collection + `Guid.Empty`, throwing the idiomatic BCL exception for each case.

```csharp
void Register(string? email, IEnumerable<string>? roles, Guid? orgId)
{
    email.ThrowIfFalsey();    // ArgumentNullException if null; ArgumentException if ""/"   "
    roles.ThrowIfFalsey();    // ArgumentNullException if null; ArgumentException if empty
    orgId.ThrowIfFalsey();    // ArgumentNullException if null; ArgumentException if Guid.Empty
    // ...
}
```

**BCL exception split** — mirrors what the BCL itself would throw for the two distinct "bad argument" cases:

| Input | Exception thrown |
| ----- | ---------------- |
| literal `null` | `ArgumentNullException` |
| `""` / whitespace-only string | `ArgumentException` ("required") |
| empty collection | `ArgumentException` ("required") |
| `Guid.Empty` | `ArgumentException` ("required") |

**Parameter name capture** — `[CallerArgumentExpression]` auto-infers the parameter name at call sites. Pass an explicit `paramName` only for indexed or computed sites:

```csharp
for (int i = 0; i < additionalScopes.Length; i++)
    additionalScopes[i].ThrowIfFalsey(paramName: $"additionalScopes[{i}]");
```

**Carve-outs** — use BCL `ThrowIfNull` (not `ThrowIfFalsey`) for:

- Plain reference-type null-guards: DI services / loggers / options that have no present-but-falsey concept.
- Projects that do not reference `DcsvIo.D2.Utilities` (e.g. avoid adding a reference that would introduce a dependency cycle).
- Guards requiring a bespoke `ArgumentException` message (`ThrowIfFalsey` has no custom-message overload).

At each carve-out site, add a one-line comment citing `// §5.1a carve-out: <reason>`.

## Boundary checks — `Truthy()` / `Falsey()` / `ToNullIfEmpty()`

The single most-used pair in the codebase. Null-safe extensions defined for `string?`, `IEnumerable<T>?`, `Guid`, and `Guid?`.

```csharp
string? userInput = ...;

if (userInput.Falsey())              // true for null / "" / "   " / "\t\n"
    return D2Result.ValidationFailed();

// After Falsey() returns false, the compiler considers value null-suspect, but
// you know it's set. The `!` is one of the FEW legitimate uses of null-forgiving.
var trimmed = userInput!.Trim();

// Or the canonical idiom — collapse to null at boundaries:
var stored = rawValue.ToNullIfEmpty();   // null | trimmed-non-empty
```

`ToNullIfEmpty()` is the workhorse for "convert empty/whitespace to null at every system boundary" — DB rows, proto mappings, user input. It's mandatory for keeping empty strings out of domain models.

```csharp
IEnumerable<T>? items = ...;
if (items.Falsey()) ...                   // true for null OR zero elements

Guid? id = ...;
if (id.Truthy()) ...                      // true for non-null AND non-empty
```

## Optional-string parsers — `TryParseTruthyNull(out Guid?)` / `TryParseTruthyNull<TEnum>(out TEnum?)`

The **canonical** way to parse a `Guid` or enum from optional string input. Use these instead of hand-rolled `Guid.TryParse` + null check or `Enum.TryParse` + `Enum.IsDefined` — they collapse every "missing / unparseable / empty / `Guid.Empty`" case into a single `null` outcome.

```csharp
// Guid parser — string -> Guid? (Guid.Empty maps to null)
"3fa85f64-5717-4562-b3fc-2c963f66afa6".TryParseTruthyNull(out Guid? id);
// id = Guid("3fa85f64-...")

((string?)null).TryParseTruthyNull(out Guid? id);              // id = null
"  ".TryParseTruthyNull(out Guid? id);                          // id = null
"00000000-0000-0000-0000-000000000000".TryParseTruthyNull(out Guid? id); // id = null
"not-a-guid".TryParseTruthyNull(out Guid? id);                  // id = null

// Enum parser — string -> TEnum? (case-insensitive)
"Active".TryParseTruthyNull(out Status? s);                     // s = Status.Active
"active".TryParseTruthyNull(out Status? s);                     // s = Status.Active (case-insensitive)
"Read,Write".TryParseTruthyNull(out Permission? p);             // p = Read | Write ([Flags] syntax)
((string?)null).TryParseTruthyNull(out Status? s);              // s = null
"NotADefinedMember".TryParseTruthyNull(out Status? s);          // s = null
```

**Gotcha — numeric-string pass-through**: `Enum.TryParse` accepts ANY integer literal as a value (it does NOT call `Enum.IsDefined`). So `"99999".TryParseTruthyNull<Status>(out var s)` returns `true` with `s = (Status)99999`, even though no member matches. This matches BCL behavior; `[Flags]` enums depend on it for combined-value parsing. If you need to reject undefined integer values for unflagged enums, layer your own `Enum.IsDefined` check on top.

## Display-friendly cleaners — `CleanStr()` / `CleanDisplayStr()`

`CleanStr()` trims and collapses internal whitespace runs into a single space; returns `null` when empty afterward.

```csharp
"  hello   world  ".CleanStr()           // "hello world"
"a\t\nb".CleanStr()                       // "a b"
"   ".CleanStr()                          // null
```

`CleanDisplayStr()` strips characters not allowed in display names (HTML tags, markdown syntax, brackets, quotes, backticks, `<>(){}[]"\`+=|\\`etc.) and then runs`CleanStr()`. Allowed: any Unicode-letter script, digits, spaces, hyphens, apostrophes, periods, commas.

```csharp
"<script>alert('x')</script>John Doe".CleanDisplayStr()
// "scriptalert'x'scriptJohn Doe"

"Mary-Jane O'Neil, Jr.".CleanDisplayStr()       // unchanged — all allowed
"Иван Петров".CleanDisplayStr()                  // unchanged — Cyrillic letters
"日本語名前".CleanDisplayStr()                    // unchanged — CJK letters
"@@@***".CleanDisplayStr()                       // null — nothing left after stripping
```

## `D2Result`-returning validators — `TryParseEmail()` / `TryParsePhoneNumber()`

`string?` extensions that return `D2Result<string>` carrying `TK.*` keys on failure. Compose with the smart-constructor pattern in domain layers — chain via `BubbleFail` instead of try/catch.

```csharp
"USER@EXAMPLE.COM".TryParseEmail()
// D2Result<string>.Ok("user@example.com")

"  user@example.com  ".TryParseEmail()
// D2Result<string>.Ok("user@example.com")

"noatsign".TryParseEmail()
// D2Result<string>.ValidationFailed(messages: [TK.Common.Validation.EMAIL_INVALID])

"+44 20 7946 0958".TryParsePhoneNumber()
// D2Result<string>.Ok("442079460958")    // digits only

"555-123-4567".TryParsePhoneNumber()
// D2Result<string>.Ok("5551234567")

"123456".TryParsePhoneNumber()
// D2Result<string>.ValidationFailed(messages: [TK.Common.Validation.PHONE_INVALID])
```

Length envelope: 7–15 digits (E.164's effective range after the `+`).

Used inside domain factory methods:

```csharp
public sealed record Contact
{
    public string Email { get; init; }

    private Contact(string email) => Email = email;

    public static D2Result<Contact> Create(string? rawEmail)
    {
        var emailResult = rawEmail.TryParseEmail();
        if (emailResult.BubbleOnFailure<string, Contact>(out var bubbled, out var email))
            return bubbled;

        return D2Result<Contact>.Ok(new Contact(email!));
    }
}
```

Failure messages are wire-format `TKMessage`s; the SvelteKit client renders them in the active locale via Paraglide. Server stays locale-unaware on the response path.

## Hash key composition — `GetNormalizedStrForHashing()`

Joins a `string?[]` with `|` separators after lowercasing + cleaning each part. Empty parts are preserved as empty segments so positional alignment is retained — important when callers build composite hash keys like `"city|region|country"` where any field may be missing.

```csharp
new string?[] { " Test One ", "   ", "TEST3" }.GetNormalizedStrForHashing()
// "test one||test3"

new string?[] { null, "", "  " }.GetNormalizedStrForHashing()
// "||"
```

## Hash-input normalization — `NormalizeForHash()`

Produces the canonical single-string hash-input form used for cross-script correlation digests: case-fold to uppercase → NFD-decompose → keep only Unicode Letter + Decimal-digit code points (any script) + single ASCII spaces. Diacritic-/case-/punctuation-equivalent inputs collapse to byte-identical output, so a SHA-256 of the result is a stable cross-script correlation key.

```csharp
"Café".NormalizeForHash()          // "CAFE"
"café".NormalizeForHash()          // "CAFE"  — same as above
"JOSÉ".NormalizeForHash()          // "JOSE"
"O'Neil-Jr.".NormalizeForHash()    // "ONEILJR"  — punct/symbols dropped
"Иван".NormalizeForHash()          // "ИВАН"  — Cyrillic kept
"日本語".NormalizeForHash()         // "日本語"  — CJK kept (caseless)
"💥".NormalizeForHash()            // ""  — Symbol → dropped
((string?)null).NormalizeForHash() // ""  — falsey → empty
```

**Stage-2-only contract.** This method does NOT trim or collapse internal whitespace. Leading, trailing, and multi-space runs survive unchanged. Callers that need whitespace normalization should apply `CleanStr()` (or equivalent stage-1 cleaning) before calling `NormalizeForHash()`. The stage-2-only shape is intentional: it keeps this helper byte-identical to the stage-2 normalizer used in Location's address hashing, so downstream callers that pre-clean their own way can forward to this single implementation without divergence.

**Purpose** — the canonical cross-script hash-input form for domain value objects that compute correlation `HashId` values (address-line and personal-name normalizers). Each caller applies its own stage-1 cleaning before calling this. Whitespace-only input is caught by the internal `Falsey()` guard and returns `string.Empty`.

**Relationship to `GetNormalizedStrForHashing()`** — these are two distinct algorithms with different purposes. `GetNormalizedStrForHashing(string?[])` joins multiple parts with `|` after lowercasing + whitespace-collapse (a composite-key lowercaser). `NormalizeForHash(string?)` operates on a single value with diacritic stripping + Unicode-category filter (a cross-script dedup canonicalizer). They are not interchangeable.

## Enumerable cleaning — `Clean()`

Materializes the enumerable, applies a per-element cleaner, and reshapes the result via two behavior knobs.

```csharp
var input = new[] { "keep1", "drop", "keep2" };
var cleaned = input.Clean(s => s == "drop" ? null : s);
// ["keep1", "keep2"]

// All cleaned to null + ReturnEmpty → []
input.Clean(_ => null, CleanEnumEmptyBehavior.ReturnEmpty);

// Cleaner returns null + ThrowOnNull → throws InvalidOperationException
input.Clean(_ => null, valueNullBehavior: CleanValueNullBehavior.ThrowOnNull);

// Empty input + Throw → throws ArgumentException
Array.Empty<string>().Clean(s => s, CleanEnumEmptyBehavior.Throw);
```

Two enums control behavior:

- `CleanEnumEmptyBehavior` (input or post-clean is empty): `ReturnNull` (default), `ReturnEmpty`, `Throw`.
- `CleanValueNullBehavior` (cleaner returns null for an element): `RemoveNulls` (default), `ThrowOnNull`.

The implementation calls `.ToList()` once upfront — generator-backed enumerables with side effects are enumerated exactly once.

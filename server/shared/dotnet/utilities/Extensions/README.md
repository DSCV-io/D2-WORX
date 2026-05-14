<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Utilities — Extensions

> Part of [`D2.Shared.Utilities`](../README.md).

The most-used surface in the lib. Boundary-check helpers (`Truthy`/`Falsey`/`ToNullIfEmpty`), optional-string parsers (`TryParseTruthyNull`), display cleaners, and `D2Result`-returning validators that compose into smart-constructor patterns.

| File | Contents |
|---|---|
| `StringExtensions.cs` | `Truthy()` / `Falsey()` / `ToNullIfEmpty()` / `CleanStr()` / `CleanDisplayStr()` / `TryParseEmail()` / `TryParsePhoneNumber()` / `GetNormalizedStrForHashing()`. |
| `EnumerableExtensions.cs` | `Truthy()` / `Falsey()` for `IEnumerable<T>?` + the `Clean()` helper with configurable empty/null behavior. |
| `CleanEnumEmptyBehavior.cs`, `CleanValueNullBehavior.cs` | Behavior enums for `EnumerableExtensions.Clean()`. |
| `GuidExtensions.cs` | `Truthy()` / `Falsey()` for `Guid` and `Guid?` (treats `Guid.Empty` as falsey) PLUS `string?.TryParseTruthyNull(out Guid?)` — the canonical "parse a Guid from optional string input, collapse missing/unparseable/empty to null" helper. |
| `EnumExtensions.cs` | `string?.TryParseTruthyNull<TEnum>(out TEnum?)` — case-insensitive `Enum.TryParse` wrapper that collapses missing/unparseable/empty to `null`; pass-through on numeric strings (matches BCL behavior — does NOT call `Enum.IsDefined`); supports comma-separated `[Flags]` syntax. |

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

`CleanDisplayStr()` strips characters not allowed in display names (HTML tags, markdown syntax, brackets, quotes, backticks, `<>(){}[]"\`+=|\\` etc.) and then runs `CleanStr()`. Allowed: any Unicode-letter script, digits, spaces, hyphens, apostrophes, periods, commas.

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

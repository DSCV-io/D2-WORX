<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Shared.Utilities — Attributes

> Part of [`D2.Shared.Utilities`](../README.md).

Marker attributes consumed reflectively elsewhere in the stack. Zero behavior on their own — they exist to label types/members so other infrastructure (Serilog destructuring, codegen, contract tests) can pick them up.

| File | Contents |
|---|---|
| `RedactDataAttribute.cs` | `[RedactData(Reason = ..., CustomReason = "...")]` — marker attribute consumed by the Serilog destructuring policy in `D2.Shared.ServiceDefaults`. Targets `AttributeTargets.All` (types, properties, fields, parameters). |

## `[RedactData]` attribute

Marker attribute consumed by the Serilog destructuring policy in `D2.Shared.ServiceDefaults`. Apply to types, properties, fields, parameters — anywhere PII or secrets might leak into logs/spans/metrics.

```csharp
public sealed record User
{
    public required string Id { get; init; }

    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string Email { get; init; }

    [RedactData(Reason = RedactReason.SecretInformation, CustomReason = "OAuth bearer token")]
    public required string AccessToken { get; init; }
}
```

`RedactReason` values: `Unspecified`, `PersonalInformation`, `FinancialInformation`, `SecretInformation`, `VerboseContent`, `Other`. See [`Enums/README.md`](../Enums/README.md) for the full enum.

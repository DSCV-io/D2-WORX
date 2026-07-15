<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Auth.Audiences.SourceGen

> Parent: [`public/packages/dotnet/`](../../README.md)

**Input contract:** [`public/contracts/auth-audiences/`](../../../../contracts/auth-audiences/README.md)

Roslyn incremental source generator that emits audience catalogs from `public/contracts/auth-audiences/audiences.spec.json` via `<AdditionalFiles>`.

**Dual-target** (assembly-name gate — see [`docs/SRC_GEN.md` §1.5](../../../../../docs/SRC_GEN.md#15-dual-target-dispatch--public-twin--private-extensions)):

| Consuming assembly | Emitted type | Values |
| --- | --- | --- |
| `DcsvIo.D2.Auth.Abstractions` | `Audiences` under `DcsvIo.D2.Auth.Abstractions` | public AdditionalFiles only |
| `DcsvIo.D2.Private.Auth.Abstractions.Extensions` | `ProductAudiences` under `DcsvIo.D2.Private.Auth` | public∪private AdditionalFiles |

Any other assembly → no emit. Private host PackageId is 1:1 with the public twin + `.Extensions`.

The spec file is the single source of truth for the platform's JWT-audience catalog. Every value an inbound JWT's `aud` claim can carry — including the broad internal audience every internal service accepts under the forward-unchanged model ([ADR-0022](../../../../../public/docs/adrs/0022-service-auth-mint-once-forward.md)) — and every `targetAudience` argument for the retained boundary-mint / exception token exchanges (`TokenExchangeClient.ExchangeAsync`) lives in one JSON file — no hand-written parallel constants, no per-feature drift.

**Convention**: spec-driven Roslyn IIncrementalGenerator pattern. See [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) for the framework-wide convention (file layout, diagnostic ID convention, generator anatomy, `<AdditionalFiles>` wiring).

---

## Build-time diagnostics

| ID         | Severity | Trigger                                                                                                                                         |
| ---------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `D2AUD001` | Error    | Spec file is malformed JSON or violates the schema (missing required `audiences` array, missing required per-entry `name` / `url`, wrong types) |
| `D2AUD002` | Error    | Audience name violates the C# identifier convention (must match `^[A-Z][A-Za-z0-9]*$`)                                                          |
| `D2AUD003` | Error    | Two audience entries share the exact same name (would produce a duplicate `const string`)                                                       |
| `D2AUD004` | Error    | Two audience entries share the exact same URL (would silently alias one to the other at JWT `aud` validation time)                              |
| `D2AUD005` | Error    | Audience URL is not a parseable absolute URI (would never match a real JWT `aud` claim value)                                                   |
| `D2AUD006` | Error    | No `audiences.spec.json` found in `AdditionalFiles`                                                                                             |

---

## Spec format

```json
{
  "$schema": "./schema.json",
  "audiences": [
    {
      "name": "Files",
      "url": "https://files.internal",
      "description": "D² Files service — object/blob CRUD over SeaweedFS."
    }
  ]
}
```

| Field         | Required | Description                                                                  |
| ------------- | -------- | ---------------------------------------------------------------------------- |
| `name`        | Yes      | PascalCase identifier emitted as the `const` name. `^[A-Z][A-Za-z0-9]*$`.    |
| `url`         | Yes      | Absolute URI used as the `aud` claim value. Must parse as an absolute `Uri`. |
| `description` | No       | Free-form description; emitted as XML doc on the constant.                   |

---

## Emitted shape

For each audience the generator emits:

```csharp
public static partial class Audiences
{
    /// <summary>D² Files service — object/blob CRUD over SeaweedFS.</summary>
    public const string Files = "https://files.internal";

    // ... other audiences ...

    public static bool IsKnown(string audience);
    public static string? Resolve(string name);
    public static string? ResolveByUrl(string url);
    public static IReadOnlySet<string> AllUrls { get; }
    public static IReadOnlyDictionary<string, string> ByName { get; }
}
```

`IsKnown(audience)` is the canonical inbound-validation helper — JWT validation calls it on the `aud` claim at every hop. `Resolve(name)` resolves a name like `"Files"` to its URL — used to address a `targetAudience` on a retained boundary-mint / exception token exchange. `ResolveByUrl(url)` is the inverse for telemetry / logging that wants a friendly name.

---

## Sourcegen-specific notes

Audience strings flow through the inbound JWT validator's `aud` claim check at every hop AND the `targetAudience` of the retained boundary-mint / exception `TokenExchangeClient.ExchangeAsync` calls. Spec-driving the catalog catches duplicate names, duplicate URLs, malformed URLs, and bad identifiers at compile time rather than at first JWT validation in production.

---

## References

- [`docs/SRC_GEN.md`](../../../../../docs/SRC_GEN.md) — canonical how-to-author guide for D² Roslyn source generators
- [`public/contracts/auth-audiences/schema.json`](../../../../contracts/auth-audiences/schema.json) — JSON Schema (editor-time gate)
- [`public/contracts/auth-audiences/audiences.spec.json`](../../../../contracts/auth-audiences/audiences.spec.json) — the catalog
- [`DcsvIo.D2.Auth.Abstractions`](../abstractions/README.md) — the consuming lib (where `Audiences.g.cs` lands)

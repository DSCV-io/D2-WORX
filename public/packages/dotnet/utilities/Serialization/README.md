<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# DcsvIo.D2.Utilities — Serialization

> Part of [`DcsvIo.D2.Utilities`](../README.md).

Frozen `JsonSerializerOptions` presets shared across the framework. One instance per preset, thread-safe and per-call allocation-free.

| File                   | Contents                                                                                  |
| ---------------------- | ----------------------------------------------------------------------------------------- |
| `SerializerOptions.cs` | Frozen `JsonSerializerOptions` presets — `SR_IgnoreCycles`, `SR_Web`, `SR_WebIgnoreNull`. |

## `SerializerOptions` presets

Three frozen `JsonSerializerOptions` instances — share them across the process; they're thread-safe and per-call allocation-free.

| Preset             | Property naming | Enums       | Nulls    | Cycles                               |
| ------------------ | --------------- | ----------- | -------- | ------------------------------------ |
| `SR_IgnoreCycles`  | as-declared     | as integers | included | tolerated (deduped during serialize) |
| `SR_Web`           | camelCase       | as strings  | included | not tolerated                        |
| `SR_WebIgnoreNull` | camelCase       | as strings  | omitted  | not tolerated                        |

```csharp
JsonSerializer.Serialize(dto, SerializerOptions.SR_Web);
// {"firstName":"Ada","lastName":"Lovelace","status":"Active"}

JsonSerializer.Serialize(dto, SerializerOptions.SR_WebIgnoreNull);
// {"firstName":"Ada"}    — null fields omitted

JsonSerializer.Serialize(graph, SerializerOptions.SR_IgnoreCycles);
// safe even if graph contains self-references
```

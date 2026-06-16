<!--
Copyright (c) DCSV. All rights reserved.
-->

# D2.Edge.KeyCustodian.Clients

> Parent: [`key-custodian/`](../README.md)

The curated transport boundary for the KeyCustodian module. External callers
(other Edge modules, host composition roots) import this project and
nothing deeper. The dependency law enforced by `<ProjectReference>` edges:

```
Domain  ←  App  ←  Infra  ←  (Edge Api)
                 ↑
              Clients   ←  (callers)
```

`Clients` sits below `App` in the dependency order — the same position as
`Domain`. `App` references `Clients` (handler input/output types). `Infra` and
`Api` reference `Clients` transitively through `App`. External callers reference
`Clients` only and never take a direct dep on `Domain`, `App`, or `Infra`.

---

## Contents

### Transport DTOs (generated)

The files below are emitted by the `@d2/typespec-emitters` TypeSpec emitter
from `contracts/typespec/key-custodian/key-custodian.tsp`. Do not edit by hand
— changes will be overwritten on the next `tsp compile` run.

| Generated file      | C# type(s)                                  | Operation |
| ------------------- | ------------------------------------------- | --------- |
| `GetJwksInput.g.cs` | `GetJwksInput` (parameterless record)        | `getJwks` |
| `GetJwksOutput.g.cs`| `GetJwksOutput(IReadOnlyList<Jwk> Keys)` + `Jwk` (6-field positional record) | `getJwks` |

All types live in `namespace D2.Edge.KeyCustodian.Clients`.

#### Jwk transport DTO vs domain VO

The domain `D2.Edge.KeyCustodian.Domain.ValueObjects.Jwk` uses 3 positional
constructor parameters (`Kid`, `N`, `E`) + 3 init-only properties with constant
defaults (`Kty = "RSA"`, `Use = "sig"`, `Alg = "RS256"`). The transport
`D2.Edge.KeyCustodian.Clients.Jwk` is a 6-field positional record:

```csharp
public sealed record Jwk(
    string Kid,
    string N,
    string E,
    string Kty,
    string Use,
    string Alg);
```

Both shapes have the same 6 public properties with the same names and types.
The transport DTO uses a 6-field positional constructor so callers constructing
response objects can supply all fields explicitly without relying on the domain
default values. The transport DTO lives here (in `Clients`) rather than in the
domain because callers must not take a dependency on the domain layer.

### Module façade interface (generated)

| Generated file                     | C# type                         | Location                          |
| ---------------------------------- | ------------------------------- | --------------------------------- |
| `IKeyCustodianInternalApi.g.cs`    | `IKeyCustodianInternalApi`      | This project (Clients namespace)  |

`IKeyCustodianInternalApi` is the single import that host callers use to interact
with the KeyCustodian module at runtime. It lists only the operations exposed
across a boundary; internal-only operations are structurally absent.

The generated interface method signature is transport-neutral — no `HandlerOptions?`
parameter — so the same interface can back both an in-process impl (today) and a
future gRPC-client impl without modification:

```csharp
ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(GetJwksInput input, CancellationToken ct = default);
```

The façade implementation (`KeyCustodianInternalApi`) and the generated DI extension
(`KeyCustodianClientsGenerated.g.cs`) live in the `app/Application/` directory, not
here, because they reference app-layer handler interfaces.

---

## Dependencies

| Reference                | Why                                                                                       |
| ------------------------ | ----------------------------------------------------------------------------------------- |
| `D2.Shared.Result`       | `D2Result<T>` is the return type of all module façade methods                             |
| `D2.Shared.Utilities`    | Required to satisfy Tier-1 global usings injected by `server/services/Directory.Build.targets` into every service-tree `net10.0` project |

No `Domain`, `App`, or `Infra` references — by design. The dependency law is
enforced structurally.

---

## Regenerating the committed fixtures

When the TypeSpec spec (`contracts/typespec/key-custodian/key-custodian.tsp`)
changes in a way that alters the generated DTO shape:

1. Run `tsp compile contracts/typespec/key-custodian/` to emit updated files
   into `server/shared/typescript/typespec-emitters/dist/generated/`.
2. Copy the updated `GetJwksInput.g.cs` and `GetJwksOutput.g.cs` into this
   directory.
3. Update the fixture constants in
   `server/shared/typescript/typespec-emitters/tests/byte-parity.test.ts` to
   match the new content.
4. Run `pnpm run test:coverage` in the typespec-emitters package to confirm
   byte-parity tests still pass.
5. Run `dotnet build server/D2.slnx` + `dotnet test` (scoped to
   `D2.Edge.Tests`) to confirm structural validation tests pass.
6. Commit the updated fixture files and updated test constants together.

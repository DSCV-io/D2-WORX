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
| `GetOidcConfigurationInput.g.cs` | `GetOidcConfigurationInput` (parameterless record) | `getOidcConfiguration` |
| `GetOidcConfigurationOutput.g.cs` | `GetOidcConfigurationOutput(Issuer, JwksUri, IdTokenSigningAlgValuesSupported, ResponseTypesSupported, SubjectTypesSupported)` | `getOidcConfiguration` |
| `SignInput.g.cs`    | `SignInput(string KeyDomain, byte[] SigningInput)` — `SigningInput` carries `[RedactData]` | `sign` |
| `SignOutput.g.cs`   | `SignOutput(string Signature, string Kid)`  | `sign` |
| `GetKeyringInput.g.cs` | `GetKeyringInput(string KeyDomain)`         | `getKeyring` |
| `GetKeyringOutput.g.cs`| `GetKeyringOutput(string ActiveKid, IReadOnlyList<KeyringEntry> Entries, byte[] AadContext)` + nested `KeyringEntry(string Kid, byte[] KeyBytes)` — `KeyBytes` (the raw AES-256 key) carries `[RedactData(SecretInformation)]` so it is masked in logs; `AadContext` is deliberately NOT redacted (authenticated-not-secret AEAD context, the UTF-8 bytes of `"d2/<domain>"`) | `getKeyring` |

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

| Generated file                  | C# type                      | Location                          |
| ------------------------------- | ---------------------------- | --------------------------------- |
| `IKeyCustodianApi.g.cs`         | `IKeyCustodianApi`           | This project (Clients namespace)  |

`IKeyCustodianApi` is the single import that host callers use to interact
with the KeyCustodian module at runtime. It lists only the operations exposed
across a boundary; internal-only operations are structurally absent.

The generated interface method signature is transport-neutral — no `HandlerOptions?`
parameter — so the in-process implementation backs it directly, and a gRPC-client
implementation would satisfy the same signature without modification:

```csharp
ValueTask<D2Result<GetJwksOutput?>> GetJwksAsync(GetJwksInput input, CancellationToken ct = default);
ValueTask<D2Result<GetOidcConfigurationOutput?>> GetOidcConfigurationAsync(GetOidcConfigurationInput input, CancellationToken ct = default);
ValueTask<D2Result<SignOutput?>> SignAsync(SignInput input, CancellationToken ct = default);
ValueTask<D2Result<GetKeyringOutput?>> GetKeyringAsync(GetKeyringInput input, CancellationToken ct = default);
```

The façade implementation (`KeyCustodianApi`) and the generated DI extension
(`KeyCustodianClientsGenerated.g.cs`) live in the `app/Application/` directory, not
here, because they reference app-layer handler interfaces.

### Minter-capability seam — `IJwtSigningCapability` (hand-authored)

The one hand-authored (non-generated) type in this project. The dedicated
cluster-signing-root (`jwks-signing`) capability — possession IS the authority
([ADR-0025](../../../../../docs/adrs/0025-request-context-establishment.md)):

```csharp
public interface IJwtSigningCapability
{
    ValueTask<D2Result<SignOutput>> SignJwtAsync(SignInput input, CancellationToken ct = default);
}
```

Registered ONLY by the JWT minter's (auth module's) composition via
`AddD2JwtSigningCapability()` — never by `AddD2KeyCustodianClients()`, the
registration every ordinary consumer of the KeyCustodian client uses. The general
`IKeyCustodianApi.SignAsync` surface can never sign `jwks-signing` for anyone —
only a holder of this capability can. The seam reuses the generated `SignInput` /
`SignOutput` transport DTOs above (no hand-authored spec-mirror type). The
`keyDomain` field on the passed `SignInput` is ignored — the minter always
targets the fixed cluster-signing root. The implementation (`JwtSigningCapability`)
and its isolated DI registration (`JwtSigningCapabilityServiceCollectionExtensions`)
live in `app/Application/`, alongside the general façade.

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

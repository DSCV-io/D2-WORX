<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Private.Edge.KeyCustodian.Client

> Parent: [`key-custodian/`](../README.md)

The consumer-facing client package for the KeyCustodian module — the curated
transport boundary PLUS the rotation-aware keyring consumer runtime. External
callers (other Edge modules, host composition roots, other services) import this
package and nothing deeper. The dependency law enforced by `<ProjectReference>`
edges:

```
Domain  ←  App  ←  Infra  ←  (Edge Api)
                 ↑
              Client   ←  (callers)
```

`Client` sits below `App` in the dependency order — the same position as
`Domain`. `App` references `Client` (handler input/output types + the keyring
hot-swap machinery its in-process source composes). `Infra` and `Api` reference
`Client` transitively through `App`. External callers reference `Client` only
and never take a direct dep on `Domain`, `App`, or `Infra`. Service-client
runtime code lives HERE, never under `public/packages/` (shared holds only
service-agnostic abstractions).

---

## Contents

### Transport DTOs (generated)

The files below are emitted by the `@dcsv-io/d2-typespec-emitters` TypeSpec emitter
from `contracts/typespec/key-custodian/key-custodian.tsp`. Do not edit by hand
— changes will be overwritten on the next `tsp compile` run.

Each op's DTOs live in a **concern folder** (folder = namespace `DcsvIo.D2.Private.Edge.KeyCustodian.Client.<Concern>`), co-located with the hand-written runtime that serves that concern. The concern is driven by the `@d2Concern("<Segment>")` decorator on the op in the `.tsp` (see [SRC_GEN.md](../../../../../docs/SRC_GEN.md)); a codegen-input change regenerates every `.g.cs` + consumer using.

| Generated file      | C# type(s)                                  | Operation |
| ------------------- | ------------------------------------------- | --------- |
| `Jwks/GetJwksInput.g.cs` | `GetJwksInput` (parameterless record)        | `getJwks` |
| `Jwks/GetJwksOutput.g.cs`| `GetJwksOutput(IReadOnlyList<Jwk> Keys)` + `Jwk` (6-field positional record) | `getJwks` |
| `OidcConfiguration/GetOidcConfigurationInput.g.cs` | `GetOidcConfigurationInput` (parameterless record) | `getOidcConfiguration` |
| `OidcConfiguration/GetOidcConfigurationOutput.g.cs` | `GetOidcConfigurationOutput(Issuer, JwksUri, IdTokenSigningAlgValuesSupported, ResponseTypesSupported, SubjectTypesSupported)` | `getOidcConfiguration` |
| `Signing/SignInput.g.cs`    | `SignInput(string KeyDomain, byte[] SigningInput)` — `SigningInput` carries `[RedactData]` | `sign` |
| `Signing/SignOutput.g.cs`   | `SignOutput(string Signature, string Kid)`  | `sign` |
| `Keyring/GetKeyringInput.g.cs` | `GetKeyringInput(string KeyDomain)`         | `getKeyring` |
| `Keyring/GetKeyringOutput.g.cs`| `GetKeyringOutput(string ActiveKid, IReadOnlyList<KeyringEntry> Entries, byte[] AadContext)` + nested `KeyringEntry(string Kid, byte[] KeyBytes)` — `KeyBytes` (the raw AES-256 key) carries `[RedactData(SecretInformation)]` so it is masked in logs; `AadContext` is deliberately NOT redacted (authenticated-not-secret AEAD context, the UTF-8 bytes of `"d2/<domain>"`) | `getKeyring` |
| `Issuance/IssueLeafInput.g.cs`  | `IssueLeafInput(byte[] CsrDer)` — a PKCS#10 CSR is PUBLIC material by construction (public key + metadata + self-signature, never a private key), so it is deliberately NOT redacted | `issueLeaf` |
| `Issuance/IssueLeafOutput.g.cs` | `IssueLeafOutput(byte[] CertificateDer, byte[] IssuerCertificateDer, DateTimeOffset NotBefore, DateTimeOffset NotAfter)` — all-public: the leaf + issuing-intermediate certificates and the validity window; **no private key exists anywhere on the issuance wire** (the workload generates its own keypair — CSR flow) | `issueLeaf` |
| `CaCertificate/GetCaCertificateInput.g.cs` | `GetCaCertificateInput` (parameterless record) | `getCaCertificate` |
| `CaCertificate/GetCaCertificateOutput.g.cs`| `GetCaCertificateOutput(byte[] RootCertificateDer, byte[] IntermediateCertificateDer)` — public trust anchor / chain material (presented on the wire in every TLS handshake), deliberately NOT redacted | `getCaCertificate` |

The generated module façade interface `IKeyCustodianApi` lives in `Facade/IKeyCustodianApi.g.cs` (`namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Facade`), importing each op's concern namespace.

#### Jwk transport DTO vs domain VO

The domain `DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects.Jwk` uses 3 positional
constructor parameters (`Kid`, `N`, `E`) + 3 init-only properties with constant
defaults (`Kty = "RSA"`, `Use = "sig"`, `Alg = "RS256"`). The transport
`DcsvIo.D2.Private.Edge.KeyCustodian.Client.Jwks.Jwk` is a 6-field positional record:

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
default values. The transport DTO lives here (in `Client`) rather than in the
domain because callers must not take a dependency on the domain layer.

### Module façade interface (generated)

| Generated file                  | C# type                      | Location                          |
| ------------------------------- | ---------------------------- | --------------------------------- |
| `IKeyCustodianApi.g.cs`         | `IKeyCustodianApi`           | This project (Client namespace)   |

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
ValueTask<D2Result<IssueLeafOutput?>> IssueLeafAsync(IssueLeafInput input, CancellationToken ct = default);
ValueTask<D2Result<GetCaCertificateOutput?>> GetCaCertificateAsync(GetCaCertificateInput input, CancellationToken ct = default);
```

The façade implementation (`KeyCustodianApi`) and the generated DI extension
(`KeyCustodianClientGenerated.g.cs`, method `AddD2KeyCustodianClient()`) live in the
`app/Application/Facade/` directory, not here, because they reference app-layer
handler interfaces.

### Minter-capability seam — `IJwtSigningCapability` (hand-authored, `Signing/`)

The hand-authored seam co-located with the `Signing/` concern (beside the generated
Sign DTOs). The dedicated cluster-signing-root (`jwks-signing`) capability —
possession IS the authority
([ADR-0025](../../../../../public/docs/adrs/0025-request-context-establishment.md)):

```csharp
public interface IJwtSigningCapability
{
    ValueTask<D2Result<SignOutput>> SignJwtAsync(SignInput input, CancellationToken ct = default);
}
```

Registered ONLY by the JWT minter's (auth module's) composition via
`AddD2JwtSigningCapability()` — never by `AddD2KeyCustodianClient()`, the
registration every ordinary consumer of the KeyCustodian client uses. The general
`IKeyCustodianApi.SignAsync` surface can never sign `jwks-signing` for anyone —
only a holder of this capability can. The seam reuses the generated `SignInput` /
`SignOutput` transport DTOs above (no hand-authored spec-mirror type). The
`keyDomain` field on the passed `SignInput` is ignored — the minter always
targets the fixed cluster-signing root. The implementation (`JwtSigningCapability`)
and its isolated DI registration (`JwtSigningCapabilityServiceCollectionExtensions`)
live in `app/Application/Signing/`.

### gRPC wire stubs (compile-once)

Physical `.g.proto` files live under `edge/api/Protos/KeyCustodian/`. This Client
package is the sole Grpc.Tools owner for **keyring + seal public + own seal private**
(`GrpcServices="Both"`). Sign / issue / cacert compile once on Edge.Api. Never dual
`<Protobuf>` the same file (CS0433). Thin server services that extend the bases live
under `edge/api/Grpc/KeyCustodian/`.

### Keyring consumer runtime (hand-authored, `Keyring/`)

The rotation-aware KeyCustodian keyring consumer — the hand-authored runtime
co-located in `Keyring/` beside the generated GetKeyring DTOs. It turns the keyring-distribution
surface (the compiled `KeyCustodianKeyring` gRPC client stub below, or the in-process
leaf) into a hot-swappable, least-privilege `IPayloadCrypto` capability: a
domain-scoped payload crypto whose key material lives in in-process memory only,
refreshes atomically when KeyCustodian announces a rotation, and is never returned,
cached at rest, or logged.

```csharp
// Separate process (dials KeyCustodian over the mutual-TLS gRPC channel):
services.AddD2EncryptionForViaKeyring("audit");

// In-host module (no network hop) — the sibling source lives in app/Application/:
services.AddD2EncryptionFromKeyCustodian("audit", callingModuleId: "edge");

// Then, anywhere a domain's payload crypto is needed:
public sealed class AuditWriter([FromKeyedServices("audit")] IPayloadCrypto crypto) { … }
```

| File                                    | Contents                                                                                                                                                 |
| --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IKeyringClient.cs`                     | `internal` raw-keyring fetch seam — a raw keyring is unreachable outside this package (+ the module App via the same-module internals grant).            |
| `GrpcKeyringClient.cs`                  | `internal` cross-process source — calls the keyring gRPC surface, maps the result envelope + reply into a `PayloadCryptoKeyring`.                        |
| `KeyringOutputMapper.cs`                | The one boundary mapper where the wire shape becomes the crypto primitive; malformed payloads surface as typed failures.                                 |
| `IRotationEventChannel.cs` + `RabbitMqRotationEventChannel.cs` | Per-domain rotation fan-out; callbacks are isolated (one throwing does not stop siblings).                                                |
| `KeyringRefreshSubscriber.cs`           | `[MqSub]` handler on the `KeyRotatedEvent` fanout; fans each event into the channel's matching-domain callbacks.                                         |
| `KeyringBackedPayloadCrypto.cs`         | The sealed hot-swap capability: single-volatile-holder swap, grace-delayed off-thread zeroize, fail-loud startup, bounded keep-serving-current refresh.  |
| `KeyringServiceCollectionExtensions.cs` | `AddD2EncryptionForViaKeyring` + the internal shared registration both sources compose; marks provenance `KeyCustodian` for the deny-by-default guard.   |
| `KeyringLog.cs` / `KeyringMetrics.cs`   | `[LoggerMessage]` delegates (domain + error code only — never key bytes, never an `Exception`) + fetch / refresh-failure / hot-swap counters.            |
| `KeyringEntry.Redaction.cs`             | Type-level `[RedactData(SecretInformation)]` partial for the generated wire proto `KeyringEntry` (protoc output cannot carry the attribute itself); named after the type it extends, and keeps the proto namespace `D2.Services.Protos.KeyCustodian.V2Alpha` so it merges with the generated partial. |

Design guarantees: key material never rests in Redis or any shared cache tier (the
keyring is itself the material protecting cache-bound data); external consumers get
ONLY the keyed `IPayloadCrypto` (`Encrypt`/`Decrypt`/`DisposeAsync` — no member ever
returns the keyring or raw key bytes); authority is enforced KeyCustodian-side at
workload granularity by the fail-closed keyring authority rule on EVERY fetch path;
the `(keyring, crypto)` pair swaps as one volatile reference (no torn reads); a frame
encrypted under the previous active kid still decrypts during the retiring-key
overlap; a refresh failure is retried bounded-with-backoff inside the subscriber
callback, then keeps serving the current keyring LOUDLY (warning + refresh-failure
metric; the next rotation event or restart re-drives); the initial fetch is a
fail-loud blocking boot fetch (no serving-before-ready window); a swapped-out keyring
zeroizes after a short off-thread grace, and `DisposeAsync` drains + force-zeroizes
anything still in grace at shutdown.

The in-process source (`InProcessKeyringClient` + `AddD2EncryptionFromKeyCustodian`)
lives in `app/Application/` — it composes the leaf `IKeyCustodianApi`, which this
package cannot reference under the dependency law.

### Sealing consumer runtime (hand-authored, `Sealing/`)

The sealed (asymmetric) sibling of the keyring runtime — the producer/consumer twins for
the version-2 **sealed** encryption mode (per-consumer-service ECDH, a compile-time
capability split). ONE spec-driven call wires everything:

```csharp
// Separate process (dials KeyCustodian over the mutual-TLS gRPC channel): wires a keyed
// IPayloadSealer for every sealed-domain consumer service, and — only when THIS service is
// itself a sealed consumer — a keyed IPayloadOpener under its own id (structural least-privilege).
services.AddD2SealedEncryptionViaKeyCustodian(ownServiceId: "audit");

// In-host module (no network hop) — the sibling source lives in app/Application/Sealing/.
// SEALER arms only: no in-process opener source exists anywhere (decrypt is cross-process-only).
services.AddD2SealedEncryptionFromKeyCustodian(ownServiceId: "audit", callingModuleId: "edge");
```

| File                                    | Contents                                                                                                                                                 |
| --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ISealingClient.cs`                     | `internal` raw seal-keyring fetch seam (public + own-private); a raw private keyring never leaves this package.                                          |
| `GrpcSealingClient.cs`                  | `internal` cross-process source over the two promoted seal gRPC stubs; maps envelope + reply into a `RecipientPublicKeyring` / `RecipientPrivateKeyring`. |
| `SealingOutputMapper.cs`                | The one boundary mapper (proto → redacted leaf DTO → keyring); the private mapper zeroizes intermediate PKCS#8 copies once the keyring copies them.       |
| `KeyringBackedPayloadOpener.cs`         | Private-keyring hot-swap opener (`Open` only): fail-loud boot fetch, rotation swap + old-kid overlap, grace-delayed off-thread zeroize.                   |
| `KeyringBackedPayloadSealer.cs`         | Public-keyring hot-swap sealer (`Seal` only): LAZY first-fetch (no boot fetch), a failed fetch → thrown retryable publish failure (never plaintext), rotation swap (no zeroize — public material). |
| `SealingServiceCollectionExtensions.cs` | `AddD2SealedEncryptionViaKeyCustodian` (public single call) + the internal sealer/opener building blocks it composes; marks provenance `KeyCustodian`.    |
| `SealingLog.cs` / `SealingMetrics.cs`   | `[LoggerMessage]` delegates (`seal:<id>` domain + error code only) + fetch / refresh-failure / hot-swap counters on the shared client meter.              |
| `SealDomainName.cs`                     | The `seal:<serviceId>` rotation-domain / metric-tag family (mirrors `KeyDomain.SEAL_PREFIX`, unreachable here under the dependency law).                  |

The capability split is compile-time: an `IPayloadSealer` has no `Open` and an
`IPayloadOpener` has no `Seal`, so a producer can never open any sealed frame including
its own. The opener's private key selection is gated KeyCustodian-side by the
authenticated mTLS peer identity (the DI shape is hygiene, not the wall). The in-process
source (`InProcessSealingClient` + `AddD2SealedEncryptionFromKeyCustodian`) lives in
`app/Application/Sealing/` and registers SEALER arms only — its own-private-key method
throws (no in-process unwrap exists).

### Telemetry

The keyring runtime publishes an OpenTelemetry meter `DcsvIo.D2.Private.Edge.KeyCustodian.Client`
(`KeyringMetrics.METER_NAME`); a host adds it via
`.WithMetrics(m => m.AddMeter(KeyringMetrics.METER_NAME))`. Counters carry closed-set,
named-constant tag keys/values — never key material:

| Counter                         | Tags                  | Meaning                                                                          |
| ------------------------------- | --------------------- | ------------------------------------------------------------------------------- |
| `d2.keyring.fetches`            | `domain`, `result`    | Every keyring fetch attempt + its outcome (`success` / `failure`).              |
| `d2.keyring.refresh_failures`   | `domain`, `errorCode` | Rotation refreshes that exhausted the bounded retry budget (serving-current meanwhile). |
| `d2.keyring.rotation_hot_swaps` | `domain`              | Successful atomic rotation hot-swaps.                                            |
| `d2.sealing.fetches`            | `domain`, `result`    | Every seal-keyring fetch (public + own-private) + its outcome.                   |
| `d2.sealing.refresh_failures`   | `domain`, `errorCode` | Seal-keyring rotation refreshes that exhausted the bounded retry budget.         |
| `d2.sealing.rotation_hot_swaps` | `domain`              | Successful seal-keyring rotation hot-swaps (`domain` = `seal:<serviceId>`).       |

`KeyringLog` (9570–9573) + `SealingLog` (9574–9578) `[LoggerMessage]` delegates occupy
the KeyCustodian Client EventId range **9570–9579**, distinct from App (9500–9529), Infra
(9530+), and Mtls (9560+). Every delegate carries the domain + an error code only, never
key material and never an `Exception` parameter.

---

## Dependencies

| Reference                | Why                                                                                       |
| ------------------------ | ----------------------------------------------------------------------------------------- |
| `DcsvIo.D2.Result`       | `D2Result<T>` is the return type of all module façade methods                             |
| `DcsvIo.D2.Utilities`    | Required to satisfy Tier-1 global usings injected by `private/services/Directory.Build.targets` into every service-tree `net10.0` project |
| `DcsvIo.D2.Result.Grpc`  | The `D2ResultProto` envelope + `AsyncUnaryCall.HandleAsync()` decode the keyring gRPC reply rides |
| `DcsvIo.D2.Encryption`   | `IPayloadCrypto` / `PayloadCryptoKeyring` (the hot-swapped primitive) + the source-provenance marker API |
| `DcsvIo.D2.Messaging.Abstractions` | `[MqSub]` / `AddD2Subscriber` for the rotation refresh subscriber                |
| `DcsvIo.D2.Handler`      | `BaseHandler` / `HandlerContext` for the refresh subscriber                                |
| `DcsvIo.D2.Auth.Events`  | `KeyRotatedEvent`, the fanout the refresh subscriber consumes                              |
| `DcsvIo.D2.Resilience`   | `RetryHelper.RetryD2ResultAsync` — the bounded, transient-classified rotation-refresh retry |

No `Domain`, `App`, or `Infra` references — by design. The dependency law is
enforced structurally.

---

## Regenerating the committed fixtures

When the TypeSpec spec (`contracts/typespec/key-custodian/key-custodian.tsp`)
changes in a way that alters the generated DTO shape:

1. Run `tsp compile contracts/typespec/key-custodian/` to emit updated files
   into `public/packages/typescript/typespec-emitters/dist/generated/`.
2. Copy the updated `GetJwksInput.g.cs` and `GetJwksOutput.g.cs` into this
   directory.
3. Update the fixture constants in
   `public/packages/typescript/typespec-emitters/tests/byte-parity.test.ts` to
   match the new content.
4. Run `pnpm run test:coverage` in the typespec-emitters package to confirm
   byte-parity tests still pass.
5. Run `dotnet build D2.slnx` + `dotnet test` (scoped to
   `DcsvIo.D2.Private.Edge.Tests`) to confirm structural validation tests pass.
6. Commit the updated fixture files and updated test constants together.

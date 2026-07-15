<!--
Copyright (c) DCSV. All rights reserved.
-->

# DcsvIo.D2.Spiffe

> Parent: [`public/packages/dotnet/`](../README.md)

The single home for the SPIFFE workload-identity grammar used by D2-WORX mutual TLS. Provides the `SpiffeWorkloadIdentity` value object — a strong-typed representation of the subject-alternative-name a leaf certificate carries and a peer validator checks: `spiffe://d2.internal/workload/<service>`.

This is a leaf-tier value object: parse, validate, and emit the SPIFFE SAN string. It holds no X.509 handles, no network code, and no framework references — certificate extraction is the caller's responsibility.

---

## Public API surface

| Type                                        | Role                                                                                                                                      |
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `SpiffeWorkloadIdentity.Create(serviceId)`  | Validates a raw service identifier and returns a `D2Result<SpiffeWorkloadIdentity>`. Rejects null / empty / non-lowercase-DNS-label input. |
| `SpiffeWorkloadIdentity.Parse(uri)`         | Validates a full SPIFFE URI from a certificate's SAN. Checks scheme, trust domain, path prefix, then delegates service-id validation to `Create`. |
| `SpiffeWorkloadIdentity.FromTrusted(serviceId)` | Skips validation for trusted store-side rehydration. Throws on null/empty. Never use with untrusted input. |
| `SpiffeWorkloadIdentity.Uri`                | The computed SPIFFE URI: `spiffe://d2.internal/workload/<ServiceId>`.                                                                    |
| `SpiffeWorkloadIdentity.ServiceId`          | The normalized lowercase service identifier (e.g. `edge`, `files`).                                                                      |

### Wire-format constants

```csharp
SpiffeWorkloadIdentity.SCHEME           // "spiffe"
SpiffeWorkloadIdentity.TRUST_DOMAIN     // "d2.internal"
SpiffeWorkloadIdentity.WORKLOAD_PATH_PREFIX  // "/workload/"
```

---

## Validation rules

`Create` and `Parse` enforce the same service-identifier rules:

- Non-null, non-empty, non-whitespace.
- Maximum length: 64 characters.
- Lowercase DNS-label-safe charset: `[a-z0-9-]` only.

`Parse` additionally asserts the full SPIFFE URI shape:

- Scheme is `spiffe`.
- Host is `d2.internal` (the D2 trust domain).
- Path begins with `/workload/`.

Both paths return a generic `D2Result.ValidationFailed` on rejection — a default-deny posture that does not reveal which check failed.

---

## Design notes

**One grammar, two consumers.** The SPIFFE format lives here and nowhere else:

- `KeyCustodian` delegates to `Create` at issuance time, re-mapping the generic `ValidationFailed` to its own `KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY` error code.
- The shared mTLS peer validator in `DcsvIo.D2.AspNetCore` calls `Parse` to check a presented certificate's URI SAN.

**No framework dependency.** This lib references only `DcsvIo.D2.Result` (for `D2Result<T>`) and `DcsvIo.D2.Utilities` (for `ToNullIfEmpty()` / `ThrowIfFalsey()`). No AspNetCore, Kestrel, X.509, or service-domain dependency.

**Not PII.** A workload identity is a service label such as `edge` or `files` — not personally identifying. The `[RedactData]` attribute is deliberately absent from this type.

---

## References

- [`DcsvIo.D2.AspNetCore`](../aspnetcore/README.md) — hosts the mTLS peer validator that feeds presented certificate SANs to `Parse`
- [ADR-0023](../../../../public/docs/adrs/0023-mtls-workload-identity.md) — mTLS workload identity for cross-process hops

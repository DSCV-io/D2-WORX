<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/packages

Never-export product package hosts under `dotnet/`. Open shared libs live under
[`public/packages/`](../../public/packages/README.md).

## DcsvIo.D2.Private.*.Extensions (1:1 with public generated surfaces)

Private dual-target hosts use PackageId form **`DcsvIo.D2.Private.<Rest>.Extensions`**
(closed marker **required** — never open-looking `DcsvIo.D2.*.Extensions`).
Each host ProjectReferences its public twin, hosts the matching dual-target
generator(s), and emits **distinct** `Product*` types under `DcsvIo.D2.Private.*`
(public∪private values). Never multi-concern bags; never non-Extensions twin brands.

| Public package | Private Extensions (PackageId = AssemblyName) | Physical home |
| --- | --- | --- |
| `DcsvIo.D2.Auth.Abstractions` | `DcsvIo.D2.Private.Auth.Abstractions.Extensions` | `dotnet/auth/abstractions-extensions/` |
| `DcsvIo.D2.Encryption` | `DcsvIo.D2.Private.Encryption.Extensions` | `dotnet/encryption/extensions/` |
| `DcsvIo.D2.I18n.Keys` | `DcsvIo.D2.Private.I18n.Keys.Extensions` | `dotnet/i18n/keys-extensions/` |

`IsPackable=false` (private packages props). Not on `public/D2.Public.slnx`.
Package-law suite: `dotnet/tests/DcsvIo.D2.Private.Packages.Tests`. Dual-target law:
[`docs/SRC_GEN.md` §1.5](../../docs/SRC_GEN.md#15-dual-target-dispatch--public-twin--private-extensions).

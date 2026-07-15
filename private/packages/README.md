<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/packages

Never-export product package hosts under `dotnet/`. Open shared libs live under
[`public/packages/`](../../public/packages/README.md).

## D2.Shared.*.Extensions (1:1 with public generated surfaces)

Private dual-target hosts mirror public PackageIds with a `.Extensions` suffix.
Each host ProjectReferences its public twin, hosts the matching dual-target
generator(s), and emits **distinct** `Product*` types under `D2.Private.*`
(public∪private values). Never multi-concern bags; never non-Extensions twin brands.

| Public package | Private Extensions (PackageId = AssemblyName) | Physical home |
| --- | --- | --- |
| `D2.Shared.Auth.Abstractions` | `D2.Shared.Auth.Abstractions.Extensions` | `dotnet/auth/abstractions-extensions/` |
| `D2.Shared.Encryption` | `D2.Shared.Encryption.Extensions` | `dotnet/encryption/extensions/` |
| `D2.Shared.I18n.Keys` | `D2.Shared.I18n.Keys.Extensions` | `dotnet/i18n/keys-extensions/` |

`IsPackable=false` (private packages props). Not on `public/D2.Public.slnx`.
Package-law suite: `dotnet/tests/D2.Private.Packages.Tests`. Dual-target law:
[`docs/SRC_GEN.md` §1.5](../../docs/SRC_GEN.md#15-dual-target-dispatch--public-twin--private-extensions).

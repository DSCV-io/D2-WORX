<!--
Copyright (c) DCSV. All rights reserved.
-->

# tools/il-fingerprint

> Parent: [`../README.md`](../README.md)

A small .NET 10 console tool that emits a **normalized, platform-independent text dump** of a built .NET assembly's metadata + IL. The release-runner's artifact-diff versioning engine hashes this dump as the **.NET output fingerprint** — the "the compiled output changed even though the public API didn't" signal that floors a release at PATCH.

## Why this exists

The versioning engine needs an "output changed" signal that is **stable across machines, OSes, and source paths**, so a baseline generated on one host equals a recompute on another. A raw DLL SHA-256 fails this requirement:

- The PE header carries a **module MVID** (a GUID derived from the compilation inputs, including the absolute source path) and a **COFF timestamp**.
- Debug-directory entries embed **absolute source paths** unless `ContinuousIntegrationBuild` / `PathMap` are set.

So a baseline hashed on one host (e.g. a Windows developer machine) would not match a recompute on another (e.g. a Linux CI runner) — the drift check would false-fail on day one for every package, and the engine would over-bump.

This tool sidesteps the problem **by construction**: it reads only the metadata tables + IL bytecode it explicitly walks, and **never reads** the MVID, the PE timestamp, or the debug-directory path entries. The cross-machine noise lives entirely in fields the dump does not emit, so the dump is path/MVID/timestamp-independent without any post-normalization.

Zero NuGet dependencies — `System.Reflection.Metadata` ships in-box on `net10.0` as part of the shared framework.

## CLI

```bash
dotnet run --project tools/il-fingerprint -c Release -- <path-to-built-dll>
```

One positional argument: the path to a built `.dll`. The normalized dump is written to **stdout** (UTF-8, LF line endings). Exit `0` on success, `2` on a missing / bad argument, `1` on a read failure.

The release-runner captures stdout and composes the final fingerprint as `SHA-256(PublicAPI.Shipped.txt + PublicAPI.Unshipped.txt + <this dump> + manifest-metadata)`.

## The normalization contract (what is emitted, what is excluded)

### Emitted (the stable signal)

- **Types**, walked from `TypeDefinitions` and **sorted by full name** (`Namespace.Name`, with nested types prefixed by their declaring type via `+`). The `<Module>` pseudo-type is skipped.
- Per type: its **attributes flags**, then its **fields** (sorted by name + decoded type) and **methods** (sorted by name + signature).
- Per member: the **fully-qualified name** + a **signature decoded to full type names** (via a custom `ISignatureTypeProvider` that renders type refs by name, never by metadata token).
- Per method body: `maxStack`, `localsInit`, and the **IL byte stream rendered as hex**. The inline-token operand of every token-bearing opcode (`call`, `callvirt`, `newobj`, `ldsfld`, `ldstr`, `ldtoken`, …) is **rewritten to the target's stable textual identity** (full member / type / type-ref name, or the escaped literal for a user-string) rather than the raw 4-byte metadata token. Branch offsets, local-var slots, and literal operands are positional / literal and rendered verbatim.

### Excluded (the cross-machine noise)

- The **module MVID** GUID.
- The **PE / COFF timestamp**.
- **Debug-directory entries** and embedded source paths.
- The **assembly version** (the package version is a separate fingerprint input via the manifest, so the IL component does not double-count a version bump).
- The hash-suffix on the compiler-synthesized `<PrivateImplementationDetails>` type name and its hash-named backing fields, which are normalized to fixed sentinels (their **presence** participates; the hash suffix does not).

## Determinism guarantees

- **Build-stability**: two builds of identical source at the same path → byte-identical dump.
- **Path / MVID / timestamp-independence**: two builds of identical source from different absolute paths / machines / OSes → byte-identical dump (the dump emits no path/MVID/timestamp field).
- **Impl-change detection retained**: a private method-body change with no public-API delta → a different dump (the IL byte stream moved).

**Honest residual**: a Roslyn / compiler-version upgrade re-emits IL and CAN change the dump. This is acceptable for a PATCH floor — a toolchain bump that changes the emitted output IS a republish-worthy change. The determinism guarantee is scoped to **same source + same toolchain + different path**, not across compiler versions.

## Validation

The dumper is exercised as a subprocess from the release-runner's gated integration tests (`D2_VERSIONING_INTEGRATION=1`): path-independence, build-stability, impl-change detection, and an MVID/timestamp-invariance negative test (two PE images differing only in those fields → identical dump). See [`../release-runner/VALIDATION.md`](../release-runner/VALIDATION.md).

## Not in the framework solution

This tool is NOT registered in `server/D2.slnx`. It's a stand-alone build-time tool invoked via `dotnet run --project`. It lives under `tools/` (outside `server/shared/dotnet`'s `Directory.Build.props` tree) and is `IsPackable=false`, so it carries no `PublicAPI.*.txt` / `.release-fingerprint` of its own — it is not a consumable.

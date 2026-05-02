<!--
Copyright (c) DCSV. All rights reserved.
-->

# tools/ — Dev Tooling

Scripts + utilities for developer workflows that aren't part of any service.

## Layout

```
tools/
  scripts/                    Shell scripts + small utilities
    gen-dev-keys.sh           Generates dev root key + per-domain encryption keys
                              Output → secrets/ (gitignored, Claude-deny-ruled)
```

## `scripts/gen-dev-keys.sh`

Generates the local-dev key material that `D2.Shared.Encryption` and `KeyCustodian` need.

```bash
./tools/scripts/gen-dev-keys.sh                    # generate any missing keys (idempotent)
./tools/scripts/gen-dev-keys.sh --rotate audit     # rotate the audit domain (new kid, old kept for grace)
./tools/scripts/gen-dev-keys.sh --force            # regenerate ALL keys (DESTRUCTIVE — invalidates encrypted data)
```

Output structure under `secrets/`:

```
secrets/
  auth/
    root.key                        Root key — encrypts all KeyCustodian keys at rest in auth_db
    audit-{yyyy}q{n}.key            Per-domain message-payload encryption keys
    notifications-{yyyy}q{n}.key
    courier-{yyyy}q{n}.key
```

All output is gitignored AND Claude-deny-ruled (`.claude/settings.json`).

## Adding new tooling

When a new dev tool / script is needed:

1. Add it to `tools/scripts/` (or a more specific subdirectory if it grows)
2. Make it executable (`chmod +x`)
3. Document it here + add a top-of-file comment block explaining what it does + how to invoke it
4. If it generates secrets / keys, output to `secrets/` (gitignored + deny-ruled)
5. If it generates non-secret artifacts, choose a sensible location (often `tools/output/` if temporary)

## What this directory is NOT

- **Not a service** — services live in `server/services/{service}/`
- **Not a shared library** — shared libs live in `server/shared/dotnet/{lib}/`
- **Not infrastructure config** — that's `infra/`
- **Not application source** — these are operator/dev tools that run once or on-demand, not part of any service's runtime

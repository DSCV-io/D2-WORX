<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/tools

Private monorepo tools and scripts (including secrets-touching helpers).

## Law

- Never place under `public/tools`.
- Secrets material writes only to monorepo-root `secrets/` (never under `private/` as a fake root).
- Resolve monorepo root via sentinel or env; fixed depth counts that assume this folder is the repo root are forbidden.

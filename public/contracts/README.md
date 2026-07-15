<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# public/contracts

**Public contracts only** — framework `$schema` files, framework value catalogs, and dual-values **public** halves.

## Law

- Public packages bind catalogs under this root only.
- Never place product-only catalogs (for example KeyCustodian error codes, product domain-lock rows, product TypeSpec) here.
- Dual-values catalogs share public schema here; product-only rows are not published in this tree.
- Documentation under this surface never cites non-export monorepo paths or operator workspaces.

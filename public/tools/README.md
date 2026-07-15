<!--
Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
-->

# public/tools

Home for **public** tools and scripts that ship with the open surface.

## Law

- Public tools resolve the monorepo root via a sentinel (or explicit env), not a fixed depth count that assumes a closed tree layout.
- Secrets generators and product-only gates never live under this tree.
- Dual-root tooling (for example contract-gate) accepts this public root by default; monorepo-only overrides never become export defaults.

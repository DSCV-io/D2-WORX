<!--
Copyright (c) DCSV. All rights reserved.
-->

# private/contracts

**Product / private contracts** home — domain catalogs, private values halves, and private-only schemas.

## Law

- Not exportable. Public packages never glob this root via `AdditionalFiles`.
- Private hosts and private generators may read public schemas plus private values.
- Advisory-lock **values** and KeyCustodian error-code catalogs live only here (engines may remain public packages).

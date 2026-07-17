<!--
Copyright (c) DCSV. All rights reserved.
-->

## 23. Configuration Hygiene
<a name="top"></a>
_[← rules index](../rules.md) · §23 of the D2-WORX rules catalog._

**Predicate index:** §23.1–§23.10 · 10 predicates.

Secrets, env vars, defaults, the `.env.local` / `.env.secrets` split, and **env-only host product config** (no `appsettings` product surface).

### Predicates — §23 configuration hygiene

- **23.1** Are env vars indexed correctly when representing a list? `PREFIX__0`, `PREFIX__1` (matching .NET `IConfiguration` array binding) AND parsed via `parseEnvArray()` in Node.
  - **Forbidden**: comma-separated lists in env vars.
  - Evidence: per array-shaped config → indexed convention.

- **23.2** Do services read env vars directly via `D2Env.Load()`? (NOT via AppHost injection — AppHost is only for container infra.)
  - Evidence: per service init → env-loading site.

- **23.3** Are secrets never committed to git? (`.env.secrets` is gitignored; `.env.secrets.example` is the template.)
  - Evidence: `git log` of `.env.secrets` → expect no commits.

- **23.4** Are new secrets added via the standard workflow?
  1. Edit `.env.secrets.example` adding `NEW_THING_API_KEY=replace_with_real_value`
  2. Update `infra/compose/compose.yml` to load it into the right service
  3. Tell the operator: "Added `NEW_THING_API_KEY` — copy into `.env.secrets`, set the real value, restart the service"
  4. Operator manually syncs (Claude cannot edit `.env.secrets` — deny rule)
  - Evidence: per new secret → workflow followed.

- **23.5** Are encryption keys generated via `private/tools/scripts/gen-dev-keys.sh` (not hand-typed)?
  - Evidence: per new key domain → generator script updated.

- **23.6** Are config defaults sane for production (not "works in dev, breaks in prod" surprises)?
  - Evidence: per Options default → production-applicability check.

- **23.7** Are config validations done at startup (fail fast) rather than on first use (fail late)?

- **23.8** Every placeholder value in `.env.local.example` / `.env.secrets.example` (and any other `*.env*.example` template under git) is realistic (`replace_with_real_value`-shape, intent-bearing format like `tw_replace_me_with_real_value` for Twilio tokens, `pk_test_replace_me` for Stripe keys), NOT `<TODO>` / `XXX` / `???` / `tbd` / empty-string placeholders.
  - **Scope**: every `.env.local.example` / `.env.secrets.example` / equivalent `*.example` template at the repo root + per-service + per-package level. Includes every key the operator is expected to populate before booting the service.
  - **Required**: each placeholder either (a) reads as a realistic shape that signals "this needs replacement" (`replace_with_real_value`, `tw_replace_me_with_real_value`, `pk_test_replace_me`, `https://your-tenant.example.com`), OR (b) carries an inline comment that names what the operator should look up.
  - **Forbidden**: bare `<TODO>` / `XXX` / `???` / `tbd` / `placeholder` / `change me` / empty-string values. These look like in-progress dev artifacts accidentally shipped — the operator can't tell whether the placeholder is intentional (waiting for them to fill) or whether the template itself is half-done.
  - **Evidence**: per `*.example` template touched in scope → grep for forbidden placeholder patterns: `grep -nEi '=[[:space:]]*(<?todo>?|xxx+|\?\?\?+|tbd|placeholder|change[_ -]me)[[:space:]]*$' <file>` → expect zero (or per-hit justification — usually fixable to a realistic placeholder).
  - **Why**: realistic placeholders signal intent (needs replacement, template finished) without looking like in-progress dev artifacts. An operator reading `.env.secrets.example` with `STRIPE_KEY=<TODO>` can't tell whether the template is complete or the author was mid-edit — the realistic-placeholder convention removes that ambiguity.
  - **How**: each `*.example` placeholder reads as a realistic shape that's obviously not a real credential — `tw_replace_me_with_real_value` (Twilio), `pk_test_replace_me` (Stripe), `https://your-tenant.auth0.com` (OAuth issuer). Pair with `private/tools/scripts/gen-dev-keys.sh` for locally-generated keys.
  - Evidence: per config-using service → startup validation.

- **23.9** **Host / service product configuration is env-only** — operators and Compose configure via `.env.local` (non-secrets) + `.env.secrets` (credentials + embedded-cred URLs) + optional Compose `environment:` rewrites (Docker DNS, in-cluster Issuer, mount paths). **Not** via product keys in `appsettings.json` / `appsettings.*.json`.
  - **Allowed in `appsettings*.json` (only):** framework noise that is not product SoT — e.g. `Logging:LogLevel`, `AllowedHosts` (if needed). Prefer **no** product sections over fake defaults.
  - **Must live in env (non-exhaustive):** `*_DATABASE_URL`, `REDIS_URL`, `RABBITMQ_URL`, Issuer base URLs, `D2_CORS_ORIGINS__*`, mTLS trust-anchor paths, Kestrel certificate paths, `KEYCUSTODIAN_*` Options, gRPC bridge addresses, host wiring when env-driven.
  - **Compose may hard-set** in-cluster values that differ from host-side localhost URLs (Redis/RMQ/PG host rewrite, Issuer `https://d2-edge:8443`) — those overrides still come from Compose env injection, not from shipping fake connection strings in appsettings.
  - **Templates:** every product key an operator must set appears in `.env.local.example` and/or `.env.secrets.example` with a realistic placeholder or safe non-secret default (§23.8). Prefer PascalCase after each `__` for Options keys (`EDGE_MTLS__TrustAnchorPath`, `KEYCUSTODIAN_INFRA__RootKeyPath`) so Linux IConfiguration maps cleanly.
  - **Evidence:** `rg -n 'DATABASE_URL|REDIS_URL|RABBITMQ|TrustAnchor|IssuerBaseUrl|RootKeyPath|Password=' private/services/**/appsettings*.json` (and any new host `appsettings*.json`) → **zero** product connection/credential/Options keys; product keys present only under env examples + Compose `environment:` / `env_file`. Spot-check host Options bind via `SECTION__Property` env form. Fail-loud at startup when required env is missing (§23.7).
  - **Why:** appsettings product keys (especially empty strings or fake passwords) **shadow or fight** env on Linux containers, leak pseudo-secrets into images, and train operators to look in the wrong place. First multiproc smoke (0030) failed when empty `TrustAnchorPath` / missing `D2_CORS_ORIGINS` were not env-complete and appsettings carried junk DB URLs.
  - **How:** strip host `appsettings.json` to Logging (+ AllowedHosts if required). Put all product knobs in `.env.local` / `.env.secrets` / Compose. Rebuild images when published `appsettings` still embeds old product keys.

- **23.10** **FORBIDDEN in `appsettings*.json` (FINDING-HIGH):** connection strings, passwords, API keys, embedded-cred URLs, empty-string product Options that exist only to be "overridden later", or any value that duplicates a secret/non-secret env SoT with a fake dev password (e.g. `"KEYCUSTODIAN_DATABASE_URL": "Host=localhost;…Password=d2"`).
  - **Also forbidden:** committing real secrets into appsettings under any environment name (`Development` included).
  - **Evidence:** same appsettings grep as §23.9; any hit with a password-like segment, `Password=`, `User ID=`, `redis://:…@`, `amqp://…:…@`, or empty `""` for a required product path = FINDING-HIGH with delete-or-move-to-env fix.
  - **Why:** baked-into-image config is not operator-editable without rebuild; fake passwords look "real enough" to ship; empty keys cause fail-closed ValidateOnStart / custom loaders to see blank before env is considered.
  - **How:** delete the key from appsettings; add/ensure the env template + Compose wiring; never reintroduce "dev defaults" in JSON for production hosts.

<sup>[↑ jump to top](#top)</sup>

---


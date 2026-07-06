<!--
Copyright (c) DCSV. All rights reserved.
-->

## 23. Configuration Hygiene
<a name="top"></a>
_[← rules index](../rules.md) · §23 of the D2-WORX rules catalog._

Secrets, env vars, defaults, and the `.env.local` / `.env.secrets` split.

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

- **23.5** Are encryption keys generated via `tools/scripts/gen-dev-keys.sh` (not hand-typed)?
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
  - **How**: each `*.example` placeholder reads as a realistic shape that's obviously not a real credential — `tw_replace_me_with_real_value` (Twilio), `pk_test_replace_me` (Stripe), `https://your-tenant.auth0.com` (OAuth issuer). Pair with `tools/scripts/gen-dev-keys.sh` for locally-generated keys.
  - Evidence: per config-using service → startup validation.

<sup>[↑ jump to top](#top)</sup>

---


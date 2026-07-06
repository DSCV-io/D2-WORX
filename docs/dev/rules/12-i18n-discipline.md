<!--
Copyright (c) DCSV. All rights reserved.
-->

## 12. i18n Discipline
<a name="top"></a>
_[← rules index](../rules.md) · §12 of the D2-WORX rules catalog._

<!-- VERBATIM-BEGIN -->

ALL user-visible strings — UI, backend handler messages (`D2Result.messages`), input errors (`D2Result.inputErrors`), notification content (D2.Courier) — go through translation keys. No hardcoded strings, not even for dev/debug pages.

### Predicates — §12 i18n discipline

- **12.1** Are all user-visible UI strings using Paraglide translations (`m.key_name()` from `$lib/paraglide/messages.js`)? Includes `<title>`, meta tags, OG tags, headings, labels, placeholders, error messages.
  - Evidence: `grep -rEn '"[A-Z][a-z][a-z]+ [a-z]' <scope .svelte files>` (English-looking literals) → per hit, justify or convert.

- **12.2** Are backend handler messages (`D2Result.messages`) and input errors (`D2Result.inputErrors`) using translation keys from `contracts/messages/` (not hardcoded strings)?
  - Evidence: per `D2Result.*` call → key reference confirmed.

- **12.3** Are D2.Courier notification templates using translation keys (not hardcoded content)?
  - Evidence: per template → key reference confirmed.

- **12.4** When adding translation keys, are they added to ALL present locale files in `contracts/messages/*.json` (kept in sync)?
  - Evidence: `ls contracts/messages/*.json | xargs jq 'keys' | sort | uniq -c` → key sets identical across locales. Run Paraglide compile from `server/web/` for frontend keys.

- **12.5** Are translation keys referenced via `TK.*` constants from `@d2/i18n` / `D2.Shared.I18n` (instead of bare TK key strings)? **Applies to TEST assertions as well as production source.** Carve-outs where the bare literal IS the API or the test (keep bare; audit treats as `⚪ N/A`, not a finding):
  - **(a) `D2Result` factory defaults** — bare strings are the API (`@d2/result` is zero-dependency).
  - **(b) i18n generation-verification tests** that pin the constant→string mapping — converting → tautology (`expect(c).toBe(c)`), testing nothing.
  - **(c) cross-language wire-contract / parity tests** — the bare literal is a drift tripwire independent of the catalog: a coordinated key rename must FAIL the test, so the assertion cannot reference the catalog (which would move in lockstep and hide the rename).
  - **(d) simulated-wire INPUT fixtures** — a raw external (e.g. .NET) payload literal entering a transform/parser; it represents external data, not our own code referencing our catalog.
  - **(e) orphan keys with no catalog entry** — test-only mock keys where no constant exists to reference.
  - Evidence: `grep -rEn '"(common|webclient|auth|geo|comms)_' <scope>` → per hit, convert OR confirm it matches carve-out (a)–(e).

- **12.6** Do new SvelteKit pages include `<svelte:head>` with translated `<title>`, `<meta name="description">`, OG tags (`og:title`, `og:description`, `og:type="website"`), `noindex` if not indexable?
  - Evidence: per new page → svelte:head block.

- **12.7** Does every navigation use `resolve("/path")` from `$app/paths` instead of bare `href="/path"` / `goto("/path")`? (Without this, i18n locale routing breaks for non-default locales.)
  - Evidence: `grep -rEn 'href="/\|goto\("/' <scope>` → per hit, confirm `resolve` wrap or justify.

- **12.8** When using SVG flags for locales, are `/static/flags/4x3/{code}.svg` assets used instead of emoji flags? (Windows doesn't render flag emoji.)
  - Evidence: per locale-flag display → SVG asset.

- **12.9** Are translation keys reused when an existing key matches the meaning?
  - Evidence: per new key → search for prior similar keys done.

<sup>[↑ jump to top](#top)</sup>

---


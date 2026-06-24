<!--
Copyright (c) DCSV. All rights reserved.
-->

# Contributing to D²-WORX

Thanks for your interest in contributing! 🎉
This project is early-stage and under active development.

## Branch Naming

Use descriptive, lowercased branches with a slash-separated prefix:

- `docs/...` for documentation and repo hygiene.
- `feat/...` for new features.
- `fix/...` for bug fixes.
- `infra/...` for CI/CD, deployment, or infrastructure changes.
- `refactor/...` for codebase cleanup without new features.
- `test/...` for test-only additions or updates.
- `chore/...` for maintenance tasks (deps, tooling, repo housekeeping).
- `wip/...` for exploratory or incomplete work.

## Commits

Follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` new feature
- `fix:` bug fix
- `docs:` documentation
- `refactor:` code change that neither fixes a bug nor adds a feature
- `test:` adding or updating tests
- `chore:` maintenance tasks

**No `Co-Authored-By` trailers** (including AI co-authors). The `commit-msg` Husky hook rejects them automatically.

## Breaking changes

D²-WORX enforces an always-on, PR-blocking breaking-change gate over its wire contracts:
proto files, spec catalogs (`contracts/**/*.spec.json`), i18n keys (`contracts/messages/*.json`),
and committed OpenAPI documents.

### Force valve

To intentionally break a stable contract, add one of these footers to a commit in the PR:

```
WIRE-BREAKING: <description>    # wire-axis (proto / OpenAPI)
BREAKING CHANGE: <description>  # api-axis (spec catalogs / i18n)
```

A `type!: subject` breaking shorthand on the Conventional-Commits subject line is also recognized.
Any breaking footer opens all gate arms for the PR.

**One conscious act — all three steps are required:**

1. Add the footer to a commit in the PR (`WIRE-BREAKING:` or `BREAKING CHANGE:`).
2. Bump the package semver MAJOR.
3. Add a `CHANGELOG.md` breaking entry describing the change and the migration path for consumers.

### Deprecate-not-delete workflow

Deleting a published spec entry — even a deprecated one — is a breaking change that requires
the force valve. The safe retirement path:

1. Keep the entry; mark it `"deprecated": true` (the gate PASSES — this is an additive change).
   Generated code gets `[Obsolete]` / `@deprecated` annotations, pushing consumers off it.
2. Once telemetry confirms the entry is unused, delete it. The gate FAILS.
   Pull the force valve (`WIRE-BREAKING:` footer) + bump MAJOR + write CHANGELOG entry.

See [docs/COMMANDS.md — Contract breaking-change gate](./docs/COMMANDS.md#contract-breaking-change-gate)
for local invocation.

## Pull Requests

- Fill out the [PR template](https://github.com/DSCV-io/D2-WORX/blob/main/.github/pull_request_template.md).
- Keep PRs focused and scoped to a single concern.
- Ensure code compiles and tests pass locally.

## Contributor Notice ⚠️

At this stage, D²-WORX is **not actively seeking external contributions**.
This repository exists primarily as a public reference implementation during its early development.

That said, if you wish to contribute, please be aware:

- The project may be **commercialized in the future**.
- The repository may be **made private** or otherwise restricted at that point.
- All contributions are accepted under the existing [PolyForm Strict License](LICENSE.md), which does **not permit commercial use**.

By submitting a contribution, you acknowledge and agree that it may become part of a future commercial product.

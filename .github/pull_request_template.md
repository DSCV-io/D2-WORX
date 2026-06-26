<!--
Copyright (c) DCSV. All rights reserved.
-->

### Summary

<!-- Provide a short description of the changes. -->

### Changes

<!-- Select the type(s) of change this PR introduces -->

- Documentation
- New feature
- Bug fix
- Refactor
- Chore
- Other (please describe)

### Details

<!-- Explain the motivation, design decisions, and any trade-offs. -->

### Testing

<!-- Describe how this change was tested or how reviewers can test it. -->

### Checklist

- [ ] Code compiles without warnings or errors
- [ ] Unit/integration tests added or updated (if applicable)
- [ ] Documentation updated (README, ADRs, etc.)
- [ ] License headers / notices applied where required
- [ ] If this PR breaks a stable contract (proto, spec catalog, i18n key, or OpenAPI doc): the `WIRE-BREAKING:` or `BREAKING CHANGE:` footer is present on the breaking commit, semver MAJOR is bumped, and a CHANGELOG.md breaking entry is added. For a breaking change to a `D2.Shared.*` / `@d2/*` library public API, the footer or `!` is also required — the gate does not auto-detect library-API breaks.
- [ ] If this PR bumps a shared library: confirm the runner dry-run shows the expected dependent bumps (propagation is on by default — dependents of a bumped package are automatically PATCH-bumped).

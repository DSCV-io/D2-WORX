<!--
Copyright (c) DCSV. All rights reserved.
-->

## 19. User Experience (UX)
<a name="top"></a>
_[← rules index](../rules.md) · §19 of the D2-WORX rules catalog._

<!-- VERBATIM-BEGIN -->

Production-ready UX means: data shows up when expected, loading states tell the user something's happening, errors are actionable, empty states make sense, the UI doesn't crash on weird inputs.

### Predicates — §19 user experience (UX)

- **19.1** Does every component displaying async / server-loaded data show a `<Skeleton>` placeholder until the data is ready?
  - Evidence: per async-data component → Skeleton confirmed.

- **19.2** Does every list / grid / table have a defined "empty state" message when there's nothing to show? (Not blank.)
  - Evidence: per list view → empty-state component.

- **19.3** Does every form field show validation errors inline (not swallowed silently and not blocking submit without explanation)?
  - Evidence: per form field → error-display surface.

- **19.4** Do form errors clear when the user fixes them (blur → re-validate → if valid, error clears)?
  - Evidence: per form → blur/clear cycle tested.

- **19.5** Does every page have a translated `<title>` so the browser tab is meaningful?
  - Evidence: per page → svelte:head title.

- **19.6** Are loading states distinguishable from empty states (don't show "no data" while still loading)?
  - Evidence: per data-display component → loading-vs-empty branching.

- **19.7** Are error states actionable? ("Something went wrong" with retry button beats a blank screen.)
  - Evidence: per error boundary → recovery action.

- **19.8** Do destructive actions (delete, irreversible state change) require explicit confirmation?
  - Evidence: per destructive action → confirm dialog.

- **19.9** Are keyboard shortcuts and accessibility attributes (aria-\*, role) present on interactive elements?
  - Evidence: per interactive component → a11y audit.

- **19.10** Does the UI work on small viewports (mobile-first responsive, or explicit non-responsive justification)?
  - Evidence: per page → viewport scan.

- **19.11** Does the UI handle slow networks (loading spinners during longer-than-skeleton operations, no double-submit, optimistic updates where appropriate)?
  - Evidence: per slow-prone operation → UX accommodation.

- **19.12** Are toast notifications used for transient feedback (success / failure / info)?
  - Evidence: per state-changing user action → toast confirmation.

- **19.13** Do pages use semantic HTML (`<nav>`, `<main>`, `<article>`, `<section>`, `<header>`, `<footer>`) so screen readers / SEO work?
  - Evidence: per page → semantic structure.

<sup>[↑ jump to top](#top)</sup>

---


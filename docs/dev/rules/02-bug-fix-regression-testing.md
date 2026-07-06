<!--
Copyright (c) DCSV. All rights reserved.
-->

## 2. Bug-Fix Regression Testing
<a name="top"></a>
_[← rules index](../rules.md) · §2 of the D2-WORX rules catalog._

Every bug fix in this scope must land with a regression test that **fails-without-fix** and **passes-with-fix** in the same change. Without it, "fixed" is unverifiable and a future refactor can silently regress the same bug.

### Predicates — §2 bug-fix regression testing

- **2.1** Does every fix made during this audit loop have a regression test?
  - Evidence: per finding fixed in any round → test file:line.
  - **No fix without a test, no exceptions.**

- **2.2** Does each regression test name encode the issue (descriptive, not audit-prefixed)? Format: `<Behavior>_<Condition>` (e.g. `MarkSeenFails_MessageRoutesToDlq_HandlerNotReplayed`, `DlqDetail_DropsExceptionMessage`, `ReadAttemptCount_OnlyCountsExpiredAndRejected`).
  - Evidence: per regression test → name format check.

- **2.3** Was the regression test confirmed-failing BEFORE the fix was applied (or, when not separable, does the test comment document the fail-without-fix expectation)?
  - Evidence: per test → confirmation note in journal. Ideally the test goes in FIRST and is confirmed failing before the fix code is applied — empirical proof the test actually covers the bug.

- **2.4** Is the test type (unit / integration) appropriate for what the bug exercises?
  - PII-safety on log delegates → reflection unit test (fast, reliable)
  - Race condition / consumer pipeline behavior / cross-service interaction → integration test (real broker / real DB)
  - Composition-time validation → unit test resolving from `ServiceProvider`
  - Evidence: per test → unit/integration classification + 1-sentence justification.

- **2.5** When a private helper needed to be made `internal` (with `[InternalsVisibleTo]`) for direct testing, is that change documented in the journal as part of the fix?
  - Evidence: per `internal` promotion → journal entry.
  - **Example**: `SubscriberChannel.ReadAttemptCount` was made `internal` so x-death-reason filter could be unit-tested across 7 cases.

<sup>[↑ jump to top](#top)</sup>

---


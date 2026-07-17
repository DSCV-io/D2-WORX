<!--
Copyright (c) DCSV. All rights reserved.
-->

## 8. Build & Tooling Hygiene
<a name="top"></a>
_[← rules index](../rules.md) · §8 of the D2-WORX rules catalog._

**Predicate index:** §8.1–§8.10 · 10 predicates.

### Predicates — §8 build & tooling hygiene

- **8.1** Was any service started manually (`dotnet run`, `pnpm dev`, `pnpm preview`, any long-running server) outside of a test that self-manages its infrastructure (Testcontainers, child processes with cleanup)?
  - **Why**: services are managed by Docker Compose; manual starts collide with the supervised processes.
  - Evidence: tool-call history check.

- **8.2** Did any host `dotnet build` run while .NET containers were active? (Crashes geo/gateway/signalr via shared `obj/` mount; always build inside container or stop all .NET containers first.)
  - Evidence: `docker compose ps` state check around build commands.

- **8.3** Did any `pnpm install` run mid-session without coordinating Node container restarts? (Rotates symlinks; breaks every Node container.)
  - Evidence: tool-call history check + container restart trace.

- **8.4** If Docker Compose was running, were affected containers verified healthy (`docker compose --env-file .env.local --env-file .env.secrets ps`) after changes? Were any unhealthy containers restarted?
  - Evidence: `ps` output + restart trace.

- **8.5** When editing shared `.NET` libs in `public/packages/dotnet/`, was `dotnet build D2.slnx` run to verify all consumers still compile?
  - **Container coordination (cross-ref §8.2)**: before running the host build with Compose up, stop active .NET containers (or build inside a container). Mirrors the §5.21 container-coordination clause; the two predicates co-apply for shared-lib edits.
  - Evidence: build output, plus `docker compose ps` snapshot showing .NET containers stopped (or build-in-container trace) when Compose was active.

- **8.6** Are dependencies / NuGet packages added intentionally? (Don't add a new dep when an existing utility / shared lib covers the need. New deps need explicit justification — security surface, license, maintenance burden.)
  - Evidence: per new `<PackageReference>` / `<dependency>` → justification + `Directory.Packages.props` entry.

- **8.7** When verifying SvelteKit / Node code, was `pnpm build` (root) used (which runs `pnpm run format && pnpm run lint && pnpm -r run build` — auto-fixing formatting, linting, then building all packages) — NOT just `pnpm format:check` + `pnpm lint` + individual `tsc`?
  - **Why**: `pnpm format:check` only reports issues without fixing them. `pnpm build` is the canonical verification step.
  - Evidence: tool-call history shows `pnpm build` invocation.

- **8.8** **Dual-suite commands.** Are both the **public-only** suite (`public/D2.Public.slnx` + public shared package tests) and the **combined umbrella** suite (root `D2.slnx` / monorepo CI) documented in `docs/COMMANDS.md` and wired in CI? Does the public-only lane require **no** private `ProjectReference` edges into `private/**`?
  - **Evidence:** COMMANDS dual-suite section + CI workflow jobs for public-only vs combined; `public/D2.Public.slnx` project graph free of `private/**` ProjectReferences.
  - **Why:** export parity — the open remote must run a suite identical to the public-only lane without private product sources.
  - **How:** keep dual-suite docs + CI jobs in lockstep; never add private ProjectReferences to Public.slnx.

- **8.9** **Export gate.** Is export of the open surface **gated** (dry-run / `workflow_dispatch` / checklist) — never a silent every-push mirror? Is the allowlist exactly `public/**` (+ any locked closed build-props extras from the dual-suites export step) and never expanded to `private/**` product trees?
  - **Evidence:** export workflow / script is dispatch-or-checklist gated; allowlist paths exclude `private/**` and monorepo-root private KEEP.
  - **Why:** accidental full-monorepo push would leak closed product IP onto the open remote.
  - **How:** dry-run first; human gate for first real push; keep allowlist reviewed on every export-tool change.

- **8.10** **Publish ownership.** Do real nuget.org / npmjs publishes **and** GitHub Releases of **public package IDs** (`DcsvIo.D2.*` open packages / `@dcsv-io/d2-*`) run only on the **`d2-public`** remote? Does the private monorepo hard-fail publish / GH-Release paths for those IDs and allow **pack + upload-artifact only**?
  - **Evidence:** private CI / release workflows lack nuget.org/npmjs push secrets for public IDs (or hard-fail publish steps); public release path owns real Release + feeds.
  - **Why:** dual-publish from private creates identity drift and leaks release authority for OSS packages.
  - **How:** private = pack/artifact; public remote = Release + registry publish.

<sup>[↑ jump to top](#top)</sup>

---


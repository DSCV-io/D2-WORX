// -----------------------------------------------------------------------
// <copyright file="job-definitions.ts" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

import type { DkronMgrConfig } from "./config.js";
import type { DesiredJob } from "./types.js";

/**
 * Minimal per-job definition. All other fields (timezone, executor, concurrency,
 * retries, disabled, executor_config defaults) are filled by `buildHttpJob`.
 */
interface JobSpec {
  readonly name: string;
  readonly displayname: string;
  /** 6-field cron: sec min hour day month weekday. */
  readonly schedule: string;
  /** Path appended to `gatewayUrl` (e.g. "/api/v1/geo/jobs/purge-stale-whois"). */
  readonly path: string;
}

/** Build a complete DesiredJob from the unique per-job fields. */
function buildHttpJob(spec: JobSpec, config: DkronMgrConfig): DesiredJob {
  return {
    name: spec.name,
    displayname: spec.displayname,
    schedule: spec.schedule,
    timezone: "UTC",
    executor: "http",
    executor_config: {
      method: "POST",
      url: `${config.gatewayUrl}${spec.path}`,
      headers: `["X-Api-Key: ${config.serviceKey}"]`,
      timeout: "30000",
      expectCode: "200",
    },
    concurrency: "forbid",
    retries: 2,
    disabled: false,
  };
}

/**
 * The desired set of Dkron jobs -- the single source of truth for what
 * jobs should exist in Dkron. To add, remove, or modify a job, edit
 * the `JOB_SPECS` array below.
 *
 * Schedule notes:
 * - 6-field cron: sec min hour day month weekday
 * - All daily, staggered by 15 minutes to avoid thundering herd on Redis locks
 * - Geo WhoIs purge runs BEFORE orphaned location cleanup (deleting WhoIs may orphan locations)
 */
const JOB_SPECS: readonly JobSpec[] = [
  // -- Geo ---------------------------------------------------------------
  {
    name: "geo-purge-stale-whois",
    displayname: "Purge WhoIs records older than retention period",
    schedule: "0 0 2 * * *",
    path: "/api/v1/geo/jobs/purge-stale-whois",
  },
  {
    name: "geo-cleanup-orphaned-locations",
    displayname: "Remove locations with zero contact and WhoIs references",
    schedule: "0 15 2 * * *",
    path: "/api/v1/geo/jobs/cleanup-orphaned-locations",
  },

  // -- Auth --------------------------------------------------------------
  {
    name: "auth-purge-sessions",
    displayname: "Purge expired auth sessions",
    schedule: "0 30 2 * * *",
    path: "/api/v1/auth/jobs/purge-sessions",
  },
  {
    name: "auth-purge-sign-in-events",
    displayname: "Purge old sign-in events beyond retention period",
    schedule: "0 45 2 * * *",
    path: "/api/v1/auth/jobs/purge-sign-in-events",
  },
  {
    name: "auth-cleanup-invitations",
    displayname: "Clean up expired org invitations",
    schedule: "0 0 3 * * *",
    path: "/api/v1/auth/jobs/cleanup-invitations",
  },
  {
    name: "auth-cleanup-emulation-consents",
    displayname: "Clean up expired or revoked emulation consents",
    schedule: "0 15 3 * * *",
    path: "/api/v1/auth/jobs/cleanup-emulation-consents",
  },
  {
    name: "auth-cleanup-deleted-users",
    displayname: "Anonymize users past the 30-day deletion grace window",
    // 04:00 UTC — runs AFTER the comms purges (03:30 + 03:45) so when this
    // job publishes `auth.user-anonymize`, downstream consumers (Geo, Comms,
    // Files in future tickets) hit a settled DB rather than racing other
    // nightly cleanup. Stays on the documented 15-min daily stagger.
    schedule: "0 0 4 * * *",
    path: "/api/v1/auth/jobs/cleanup-deleted-users",
  },

  // -- Comms -------------------------------------------------------------
  {
    name: "comms-purge-deleted-messages",
    displayname: "Purge soft-deleted messages beyond retention period",
    schedule: "0 30 3 * * *",
    path: "/api/v1/comms/jobs/purge-deleted-messages",
  },
  {
    name: "comms-purge-delivery-history",
    displayname: "Purge old delivery request and attempt history",
    schedule: "0 45 3 * * *",
    path: "/api/v1/comms/jobs/purge-delivery-history",
  },
];

/** Returns the full desired job set with URLs resolved from config. */
export function getDesiredJobs(config: DkronMgrConfig): DesiredJob[] {
  return JOB_SPECS.map((spec) => buildHttpJob(spec, config));
}

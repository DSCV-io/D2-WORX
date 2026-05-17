// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

export {
  parseAuthHeader,
  type ParseAuthHeaderOptions,
} from "./parse-auth-header.js";
export {
  parseRequestContextFromHeaders,
  type ParseRequestContextOptions,
} from "./parse-request-context.js";
export {
  toProblemDetails,
  type ProblemDetailsBody,
  type ProblemDetailsOptions,
} from "./problem-details.js";
export {
  defaultTitleForStatus,
  PROBLEM_DETAILS_CONTENT_TYPE,
  PROBLEM_TYPE_URI_PREFIX,
  ProblemDetailsExtensionKeys,
  ProblemDetailsTitles,
} from "./problem-details.g.js";
export type {
  AuthenticatedRequestContext,
  GuardRequestEvent,
  GuardThrowers,
} from "./guards/guard-types.js";
export { requireAuth } from "./guards/require-auth.js";
export { requireOrg } from "./guards/require-org.js";
export { requireRole } from "./guards/require-role.js";
export { requireScope } from "./guards/require-scope.js";
export { redirectIfAuthenticated } from "./guards/redirect-if-authenticated.js";
export type { JwtPayload } from "@d2/auth-abstractions";

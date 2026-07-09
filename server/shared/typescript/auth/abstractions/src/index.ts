// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

export {
  AuthErrorCodes,
  type AuthErrorCode,
  ALL_AUTH_ERROR_CODES,
  getAuthErrorHttpStatus,
} from "./auth-error-codes.g.js";
export { AuthFailures } from "./auth-failures.g.js";
export { Scopes, ALL_SCOPES } from "./scopes.g.js";
export {
  ProtocolAudiences,
  type ProtocolAudience,
  ALL_PROTOCOL_AUDIENCES,
} from "./protocol-audiences.g.js";
export { JwtClaimTypes, type JwtClaimType } from "./jwt-claim-types.g.js";
export type { JwtPayload } from "./jwt-payload.g.js";

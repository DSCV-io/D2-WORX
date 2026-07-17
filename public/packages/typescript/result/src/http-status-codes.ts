// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * HTTP status code numeric constants used by the result layer.
 * Mirrors the .NET `System.Net.HttpStatusCode` values D2 actually
 * surfaces — not the full RFC catalog.
 */
export const HttpStatusCode = {
  OK: 200,
  Created: 201,
  PartialContent: 206,
  BadRequest: 400,
  Unauthorized: 401,
  Forbidden: 403,
  NotFound: 404,
  Conflict: 409,
  RequestEntityTooLarge: 413,
  TooManyRequests: 429,
  InternalServerError: 500,
  ServiceUnavailable: 503,
} as const;

export type HttpStatusCode =
  (typeof HttpStatusCode)[keyof typeof HttpStatusCode];

// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Account API proxy catch-all route.
 *
 * Forwards all /api/account/* requests to the Auth service, preserving
 * cookies and headers. Uses the same authProxy as /api/auth/* since
 * both route groups are served by the Auth service's Hono server.
 */
import { D2Result } from "@d2/result";
import { getAuthContext } from "$lib/server/auth.server";
import type { RequestHandler } from "./$types";

const handler: RequestHandler = async (event) => {
  const ctx = getAuthContext();
  if (!ctx) {
    const result = D2Result.serviceUnavailable();
    return new Response(JSON.stringify(result), {
      status: result.statusCode,
      headers: { "Content-Type": "application/json" },
    });
  }
  return ctx.authProxy.proxyRequest(event);
};

export const GET = handler;
export const POST = handler;
export const PUT = handler;
export const PATCH = handler;
export const DELETE = handler;

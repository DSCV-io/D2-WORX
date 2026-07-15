// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
import type { AuthSession, AuthUser } from "$lib/server/auth-bff-stubs";

/** Minimal request-context slot — product enrichment fills at runtime. */
interface AppRequestContext {
  correlationId?: string;
  traceId?: string;
}

declare global {
  namespace App {
    interface Error {
      message: string;
      traceId?: string;
    }

    interface Locals {
      /** Populated by request enrichment middleware. */
      requestContext?: AppRequestContext;
      /** Populated by the auth hook — session from the Auth service */
      session?: AuthSession;
      /** Populated by the auth hook — user from the Auth service */
      user?: AuthUser;
    }

    // interface PageData {}
    // interface PageState {}
    // interface Platform {}
  }
}

export {};

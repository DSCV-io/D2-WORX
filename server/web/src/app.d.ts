// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// See https://svelte.dev/docs/kit/types#app.d.ts
// for information about these interfaces
import type { IRequestContext } from "@d2/handler";
import type { AuthSession, AuthUser } from "@d2/auth-bff-client";

declare global {
  namespace App {
    interface Error {
      message: string;
      traceId?: string;
    }

    interface Locals {
      /** Populated by request enrichment middleware. */
      requestContext?: IRequestContext;
      /** Populated by the auth hook — session from the Auth service */
      session?: AuthSession | null;
      /** Populated by the auth hook — user from the Auth service */
      user?: AuthUser | null;
    }

    // interface PageData {}
    // interface PageState {}
    // interface Platform {}
  }
}

export {};

/**
 * Client-side BetterAuth client for browser-side auth operations.
 *
 * Used for sign-in/sign-up form submissions, sign-out, and reactive
 * session state. All requests are proxied through SvelteKit's
 * /api/auth/* catch-all route to the Auth service.
 *
 * NOT used for server-side session resolution — that goes through
 * @d2/auth-bff-client's SessionResolver.
 *
 * During SSR the module is imported but the client is never called —
 * all usage is inside browser-only event handlers (onUpdate, onclick).
 * We still need a valid base URL to avoid createAuthClient throwing,
 * so we use window.location.origin on the client and a placeholder
 * on the server.
 */
import { browser } from "$app/environment";
import { createAuthClient } from "better-auth/svelte";

const baseURL = browser ? `${window.location.origin}/api/auth` : "http://localhost/api/auth";

export const authClient = createAuthClient({ baseURL });

import { dev } from "$app/environment";
import { error } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";

export const load: PageServerLoad = async ({ locals, request }) => {
  if (!dev) {
    error(404, "Not found");
  }

  // Extract cookie names (not values — security)
  const cookieHeader = request.headers.get("cookie") ?? "";
  const cookieNames = cookieHeader
    .split(";")
    .map((c) => c.trim().split("=")[0])
    .filter(Boolean);

  // Serialize requestContext (it's a frozen object with readonly props).
  // Every IRequestContext field is included so the debug page shows the full picture.
  const rc = locals.requestContext;
  const requestContext = rc
    ? {
        // Tracing
        traceId: rc.traceId ?? null,
        requestId: rc.requestId ?? null,
        requestPath: rc.requestPath ?? null,

        // User / Identity
        isAuthenticated: rc.isAuthenticated,
        userId: rc.userId ?? null,
        email: rc.email ?? null,
        username: rc.username ?? null,

        // Agent Organization
        agentOrgId: rc.agentOrgId ?? null,
        agentOrgName: rc.agentOrgName ?? null,
        agentOrgType: rc.agentOrgType ?? null,
        agentOrgRole: rc.agentOrgRole ?? null,

        // Target Organization
        targetOrgId: rc.targetOrgId ?? null,
        targetOrgName: rc.targetOrgName ?? null,
        targetOrgType: rc.targetOrgType ?? null,
        targetOrgRole: rc.targetOrgRole ?? null,

        // Emulation & Impersonation
        isOrgEmulating: rc.isOrgEmulating ?? false,
        isUserImpersonating: rc.isUserImpersonating ?? false,
        impersonatedBy: rc.impersonatedBy ?? null,
        impersonatingEmail: rc.impersonatingEmail ?? null,
        impersonatingUsername: rc.impersonatingUsername ?? null,

        // Network / Enrichment
        clientIp: rc.clientIp ?? null,
        serverFingerprint: rc.serverFingerprint ?? null,
        clientFingerprint: rc.clientFingerprint ?? null,
        deviceFingerprint: rc.deviceFingerprint ?? null,
        whoIsHashId: rc.whoIsHashId ?? null,
        city: rc.city ?? null,
        countryCode: rc.countryCode ?? null,
        subdivisionCode: rc.subdivisionCode ?? null,
        isVpn: rc.isVpn ?? null,
        isProxy: rc.isProxy ?? null,
        isTor: rc.isTor ?? null,
        isHosting: rc.isHosting ?? null,

        // Trust & Helpers
        isTrustedService: rc.isTrustedService ?? false,
        isAgentStaff: rc.isAgentStaff ?? false,
        isAgentAdmin: rc.isAgentAdmin ?? false,
        isTargetingStaff: rc.isTargetingStaff ?? false,
        isTargetingAdmin: rc.isTargetingAdmin ?? false,
      }
    : null;

  return {
    debugSession: locals.session ?? null,
    debugUser: locals.user ?? null,
    requestContext,
    cookieNames,
  };
};

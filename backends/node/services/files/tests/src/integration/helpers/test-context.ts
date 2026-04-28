import { HandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";

export function createTestContext(): HandlerContext {
  const request: IRequestContext = {
    traceId: "trace-integration",
    isAuthenticated: null,
    isTrustedService: null,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
    isOrgEmulating: null,
    isUserImpersonating: null,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

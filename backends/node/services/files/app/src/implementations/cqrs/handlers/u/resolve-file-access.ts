import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { Utilities } from "../../../../interfaces/cqrs/handlers/index.js";
import type { ICheckFileAccessHandler } from "../../../../interfaces/cqrs/handlers/q/check-file-access.js";

type Input = Utilities.ResolveFileAccessInput;
type Output = Utilities.ResolveFileAccessOutput;

/**
 * Resolves file access based on the context key's resolution strategy.
 *
 * - jwt_owner — requestContext.userId must match relatedEntityId
 * - jwt_org — requestContext.targetOrgId must match relatedEntityId
 * - authenticated — any authenticated user
 * - callback — delegates to outbound CheckFileAccess handler
 *
 * Fail-closed: unknown resolution types or missing data → forbidden.
 *
 * Note: No Zod validation — this is an internal utility handler called only
 * by typed app-layer handlers that validate their own inputs. The config
 * object (ContextKeyConfig) is too complex for meaningful re-validation here.
 */
export class ResolveFileAccess
  extends BaseHandler<Input, Output>
  implements Utilities.IResolveFileAccessHandler
{
  private readonly accessChecker?: ICheckFileAccessHandler;

  constructor(context: IHandlerContext, accessChecker?: ICheckFileAccessHandler) {
    super(context);
    this.accessChecker = accessChecker;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    // Per-action dispatch: upload uses uploadResolution, read uses
    // readResolution (allows `authenticated`), list uses listResolution
    // (deliberately narrower — no `authenticated` to prevent cross-tenant
    // enumeration; routed through `callback` if a contextKey wants public
    // listing).
    const resolution =
      input.action === "upload"
        ? input.config.uploadResolution
        : input.action === "list"
          ? input.config.listResolution
          : input.config.readResolution;

    const request = this.context.request;

    switch (resolution) {
      case "jwt_owner":
        if (request.isAuthenticated !== true) return D2Result.unauthorized();
        return request.userId === input.relatedEntityId ? D2Result.ok() : D2Result.forbidden();

      case "jwt_org":
        if (request.isAuthenticated !== true) return D2Result.unauthorized();
        return request.targetOrgId === input.relatedEntityId ? D2Result.ok() : D2Result.forbidden();

      case "authenticated":
        return request.isAuthenticated === true ? D2Result.ok() : D2Result.unauthorized();

      case "callback": {
        if (!this.accessChecker) return D2Result.forbidden();
        if (!request.userId) return D2Result.unauthorized();
        const result = await this.accessChecker.handleAsync({
          address: input.config.callbackAddress,
          contextKey: input.config.contextKey,
          relatedEntityId: input.relatedEntityId,
          requestingUserId: request.userId,
          requestingOrgId: request.targetOrgId,
          action: input.action,
        });
        return result.success && result.data?.allowed ? D2Result.ok() : D2Result.forbidden();
      }

      default:
        return D2Result.forbidden();
    }
  }
}

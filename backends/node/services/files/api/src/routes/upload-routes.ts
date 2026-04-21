import { Hono } from "hono";
import type { Context } from "hono";
import type { ServiceScope } from "@d2/di";
import type { IRequestContext } from "@d2/handler";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { IUploadFileKey } from "@d2/files-app";
import { TK } from "@d2/i18n";
import { cleanDisplayStr } from "@d2/utilities";
import type { ContextKeyConfigMap } from "@d2/files-app";
import { SCOPE_KEY, REQUEST_CONTEXT_KEY } from "../context-keys.js";

/**
 * Upload routes — purpose-specific endpoints that hardcode their contextKey.
 * Users never provide contextKeys directly.
 *
 * - `POST /avatar` → `user_avatar`, relatedEntityId = userId
 * - `POST /org/logo` → `org_logo`, relatedEntityId = targetOrgId (from JWT)
 * - `POST /org/documents` → `org_document`, relatedEntityId = targetOrgId (from JWT)
 * - `POST /threads/:threadId/attachments` → `thread_attachment`, relatedEntityId = threadId
 */
export function createUploadRoutes(contextKeyConfigs: ContextKeyConfigMap): Hono {
  const app = new Hono();

  app.post("/avatar", async (c) => {
    return handleUpload(c, contextKeyConfigs, "user_avatar", (rc) => rc.userId);
  });

  app.post("/org/logo", async (c) => {
    return handleUpload(c, contextKeyConfigs, "org_logo", (rc) => rc.targetOrgId, {
      missingEntityStatus: 401,
      missingEntityMessage: TK.middleware.errors.NO_ACTIVE_ORGANIZATION,
    });
  });

  app.post("/org/documents", async (c) => {
    return handleUpload(c, contextKeyConfigs, "org_document", (rc) => rc.targetOrgId, {
      missingEntityStatus: 401,
      missingEntityMessage: TK.middleware.errors.NO_ACTIVE_ORGANIZATION,
    });
  });

  app.post("/threads/:threadId/attachments", async (c) => {
    return handleUpload(c, contextKeyConfigs, "thread_attachment", () => c.req.param("threadId"));
  });

  return app;
}

interface MissingEntityOptions {
  /** HTTP status code when relatedEntityId is missing (default: 400). */
  readonly missingEntityStatus?: number;
  /**
   * Error message when relatedEntityId is missing (default:
   * `TK.files.errors.MISSING_RELATED_ENTITY`). MUST be a TK translation key —
   * the FE resolves it via `translateMessage()`. Never raw English.
   */
  readonly missingEntityMessage?: string;
}

async function handleUpload(
  c: Context,
  contextKeyConfigs: ContextKeyConfigMap,
  contextKey: string,
  getRelatedEntityId: (rc: IRequestContext) => string | undefined,
  options?: MissingEntityOptions,
): Promise<Response> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const scope = (c as any).get(SCOPE_KEY) as ServiceScope;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const requestContext = (c as any).get(REQUEST_CONTEXT_KEY) as IRequestContext;

  const relatedEntityId = getRelatedEntityId(requestContext);
  if (!relatedEntityId) {
    const status = options?.missingEntityStatus ?? 400;
    const message = options?.missingEntityMessage ?? TK.files.errors.MISSING_RELATED_ENTITY;
    return c.json(
      { success: false, statusCode: status, messages: [message], data: null },
      status as ContentfulStatusCode,
    );
  }

  if (!contextKeyConfigs.has(contextKey)) {
    return c.json(
      {
        success: false,
        statusCode: 400,
        messages: [TK.files.errors.INVALID_UPLOAD_TARGET],
        data: null,
      },
      400 as ContentfulStatusCode,
    );
  }

  let body: { contentType?: string; displayName?: string; sizeBytes?: number };
  try {
    body = await c.req.json();
  } catch {
    return c.json(
      {
        success: false,
        statusCode: 400,
        messages: [TK.files.errors.INVALID_JSON_BODY],
        data: null,
      },
      400 as ContentfulStatusCode,
    );
  }

  const handler = scope.resolve(IUploadFileKey);
  const result = await handler.handleAsync({
    contextKey,
    relatedEntityId,
    contentType: body.contentType ?? "application/octet-stream",
    displayName: cleanDisplayStr(body.displayName) ?? "unnamed",
    sizeBytes: body.sizeBytes ?? 0,
  });

  return c.json(
    {
      success: result.success,
      statusCode: result.statusCode,
      messages: result.messages,
      data: result.data ?? null,
    },
    result.statusCode as ContentfulStatusCode,
  );
}

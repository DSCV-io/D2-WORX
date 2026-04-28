import { Hono } from "hono";
import type { Context } from "hono";
import type { IRequestContext } from "@d2/handler";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { IUploadFileKey } from "@d2/files-app";
import { TK } from "@d2/i18n";
import { cleanDisplayStr } from "@d2/utilities";
import { isValidGuid } from "@d2/handler";
import type { ContextKeyConfigMap } from "@d2/files-app";
import type { FilesVariables } from "../context-keys.js";

/**
 * Upload routes — purpose-specific endpoints that hardcode their contextKey.
 * Users never provide contextKeys directly.
 *
 * - `POST /avatar` → `user_avatar`, relatedEntityId = userId
 * - `POST /org/logo` → `org_logo`, relatedEntityId = targetOrgId (from JWT)
 * - `POST /org/documents` → `org_document`, relatedEntityId = targetOrgId (from JWT)
 * - `POST /threads/:threadId/attachments` → `thread_attachment`, relatedEntityId = threadId
 */
export function createUploadRoutes(
  contextKeyConfigs: ContextKeyConfigMap,
): Hono<{ Variables: FilesVariables }> {
  const app = new Hono<{ Variables: FilesVariables }>();

  app.post("/avatar", async (c) => {
    return handleUpload(c, contextKeyConfigs, "user_avatar", (rc) => rc.userId);
  });

  app.post("/org/logo", async (c) => {
    return handleUpload(c, contextKeyConfigs, "org_logo", (rc) => rc.targetOrgId, {
      missingEntityIsUnauthorized: true,
      missingEntityMessage: TK.middleware.errors.NO_ACTIVE_ORGANIZATION,
    });
  });

  app.post("/org/documents", async (c) => {
    return handleUpload(c, contextKeyConfigs, "org_document", (rc) => rc.targetOrgId, {
      missingEntityIsUnauthorized: true,
      missingEntityMessage: TK.middleware.errors.NO_ACTIVE_ORGANIZATION,
    });
  });

  app.post("/threads/:threadId/attachments", async (c) => {
    // Validate threadId shape at the route boundary. The downstream
    // `CanAccess` callback in Comms is the real authorization gate, but a
    // UUID check here keeps malformed IDs out of the S3 key path and out of
    // the gRPC payload — both of which embed `relatedEntityId` as a string
    // segment that could otherwise carry path traversal or wildcard chars.
    const threadId = c.req.param("threadId");
    if (!isValidGuid(threadId)) {
      return c.json(
        D2Result.validationFailed({ messages: [TK.common.validation.ID_INVALID] }),
        400 as ContentfulStatusCode,
      );
    }
    return handleUpload(c, contextKeyConfigs, "thread_attachment", () => threadId);
  });

  return app;
}

/**
 * Defines what to return when `getRelatedEntityId` resolves to undefined.
 * The default ("missing param") is a 400 — but routes that derive the entity
 * from the session (org_logo / org_document) reinterpret the absence as 401
 * because it really means "no active organization in your session."
 */
interface MissingEntityOptions {
  readonly missingEntityIsUnauthorized?: boolean;
  readonly missingEntityMessage?: string;
}

async function handleUpload(
  c: Context<{ Variables: FilesVariables }>,
  contextKeyConfigs: ContextKeyConfigMap,
  contextKey: string,
  getRelatedEntityId: (rc: IRequestContext) => string | undefined,
  options?: MissingEntityOptions,
): Promise<Response> {
  const scope = c.var.scope;
  const requestContext = c.var.requestContext;

  const relatedEntityId = getRelatedEntityId(requestContext);
  if (!relatedEntityId) {
    if (options?.missingEntityIsUnauthorized) {
      const message = options.missingEntityMessage ?? TK.files.errors.MISSING_RELATED_ENTITY;
      return c.json(D2Result.unauthorized({ messages: [message] }), 401 as ContentfulStatusCode);
    }
    return c.json(
      D2Result.validationFailed({
        messages: [options?.missingEntityMessage ?? TK.files.errors.MISSING_RELATED_ENTITY],
      }),
      400 as ContentfulStatusCode,
    );
  }

  if (!contextKeyConfigs.has(contextKey)) {
    return c.json(
      D2Result.validationFailed({ messages: [TK.files.errors.INVALID_UPLOAD_TARGET] }),
      400 as ContentfulStatusCode,
    );
  }

  let body: { contentType?: string; displayName?: string; sizeBytes?: number };
  try {
    body = await c.req.json();
  } catch {
    return c.json(
      D2Result.validationFailed({ messages: [TK.files.errors.INVALID_JSON_BODY] }),
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

  return c.json(result, result.statusCode as ContentfulStatusCode);
}

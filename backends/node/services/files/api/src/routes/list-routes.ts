import { Hono } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { D2Result } from "@d2/result";
import { IListFilesKey } from "@d2/files-app";
import { TK } from "@d2/i18n";
import type { FilesVariables } from "../context-keys.js";

/**
 * List routes — paginated file listing by context.
 *
 * `GET /files?contextKey=...&relatedEntityId=...&limit=...&offset=...`
 */
export function createListRoutes(): Hono<{ Variables: FilesVariables }> {
  const app = new Hono<{ Variables: FilesVariables }>();

  app.get("/files", async (c) => {
    const scope = c.var.scope;
    const contextKey = c.req.query("contextKey");
    const relatedEntityId = c.req.query("relatedEntityId");

    if (!contextKey || !relatedEntityId) {
      return c.json(
        D2Result.validationFailed({
          messages: [TK.files.errors.LIST_QUERY_PARAMS_REQUIRED],
        }),
        400 as ContentfulStatusCode,
      );
    }

    const limit = Math.max(Math.min(Number(c.req.query("limit")) || 50, 100), 1);
    const offset = Math.max(Number(c.req.query("offset")) || 0, 0);

    const handler = scope.resolve(IListFilesKey);
    const result = await handler.handleAsync({
      contextKey,
      relatedEntityId,
      limit,
      offset,
    });

    return c.json(result, result.statusCode as ContentfulStatusCode);
  });

  return app;
}

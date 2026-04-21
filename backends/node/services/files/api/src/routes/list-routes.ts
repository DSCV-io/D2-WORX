import { Hono } from "hono";
import type { ServiceScope } from "@d2/di";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { IListFilesKey } from "@d2/files-app";
import { TK } from "@d2/i18n";
import { SCOPE_KEY } from "../context-keys.js";

/**
 * List routes — paginated file listing by context.
 *
 * `GET /files?contextKey=...&relatedEntityId=...&limit=...&offset=...`
 */
export function createListRoutes(): Hono {
  const app = new Hono();

  app.get("/files", async (c) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const scope = (c as any).get(SCOPE_KEY) as ServiceScope;
    const contextKey = c.req.query("contextKey");
    const relatedEntityId = c.req.query("relatedEntityId");

    if (!contextKey || !relatedEntityId) {
      return c.json(
        {
          success: false,
          statusCode: 400,
          messages: [TK.files.errors.LIST_QUERY_PARAMS_REQUIRED],
          data: null,
        },
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

    return c.json(
      {
        success: result.success,
        statusCode: result.statusCode,
        messages: result.messages,
        data: result.data ?? null,
      },
      result.statusCode as ContentfulStatusCode,
    );
  });

  return app;
}

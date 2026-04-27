import { Hono } from "hono";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { IGetFileVariantUrlKey } from "@d2/files-app";
import type { FilesVariables } from "../context-keys.js";

/**
 * Variant URL routes — returns presigned GET URLs for file variants.
 *
 * `GET /files/:fileId/:variantName/url` — returns a time-limited presigned URL
 * that browsers can use directly in `<img src>`. Access control, file lookup,
 * and variant validation are handled by the app-layer GetFileVariantUrl handler.
 */
export function createVariantUrlRoutes(): Hono<{ Variables: FilesVariables }> {
  const app = new Hono<{ Variables: FilesVariables }>();

  app.get("/files/:fileId/:variantName/url", async (c) => {
    const scope = c.var.scope;
    const { fileId, variantName } = c.req.param();

    const handler = scope.resolve(IGetFileVariantUrlKey);
    const result = await handler.handleAsync({ fileId, variantName });

    return c.json(result, result.statusCode as ContentfulStatusCode);
  });

  return app;
}

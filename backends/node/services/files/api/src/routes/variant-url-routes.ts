import { Hono } from "hono";
import type { ServiceScope } from "@d2/di";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { IGetFileVariantUrlKey } from "@d2/files-app";
import { SCOPE_KEY } from "../context-keys.js";

/**
 * Variant URL routes — returns presigned GET URLs for file variants.
 *
 * `GET /files/:fileId/:variantName/url` — returns a time-limited presigned URL
 * that browsers can use directly in `<img src>`. Access control, file lookup,
 * and variant validation are handled by the app-layer GetFileVariantUrl handler.
 */
export function createVariantUrlRoutes(): Hono {
  const app = new Hono();

  app.get("/files/:fileId/:variantName/url", async (c) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const scope = (c as any).get(SCOPE_KEY) as ServiceScope;
    const { fileId, variantName } = c.req.param();

    const handler = scope.resolve(IGetFileVariantUrlKey);
    const result = await handler.handleAsync({ fileId, variantName });

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

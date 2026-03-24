import { Hono } from "hono";
import type { ServiceScope } from "@d2/di";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import { IDownloadFileVariantKey } from "@d2/files-app";
import { SCOPE_KEY } from "../context-keys.js";

/**
 * Download routes — file download proxy via storage.
 *
 * `GET /files/:fileId/:variantName` — streams file content. Access control,
 * file lookup, variant validation, and storage retrieval are handled by the
 * app-layer DownloadFileVariant handler.
 *
 * Uses `Content-Disposition: attachment` to prevent XSS via uploaded SVG/HTML.
 * Sets aggressive caching for immutable content-addressable files.
 */
export function createDownloadRoutes(): Hono {
  const app = new Hono();

  app.get("/files/:fileId/:variantName", async (c) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const scope = (c as any).get(SCOPE_KEY) as ServiceScope;
    const { fileId, variantName } = c.req.param();

    const handler = scope.resolve(IDownloadFileVariantKey);
    const result = await handler.handleAsync({ fileId, variantName });

    if (!result.success || !result.data) {
      return c.json(
        {
          success: false,
          statusCode: result.statusCode,
          messages: result.messages,
          data: null,
        },
        result.statusCode as ContentfulStatusCode,
      );
    }

    const { buffer, contentType, displayName } = result.data;

    const headers = new Headers();
    headers.set("Content-Type", contentType);
    headers.set("Cache-Control", "public, max-age=31536000, immutable");
    headers.set("Content-Disposition", `attachment; filename="${displayName}"`);

    return new Response(new Uint8Array(buffer), { status: 200, headers });
  });

  return app;
}

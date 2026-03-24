import { Hono } from "hono";
import type { ServiceScope } from "@d2/di";
import type { ContentfulStatusCode } from "hono/utils/http-status";
import type { ContextKeyConfigMap } from "@d2/files-app";
import {
  IGetFileMetadataKey,
  IPresignGetUrlKey,
  IResolveFileAccessKey,
  buildVariantStorageKey,
} from "@d2/files-app";
import { SCOPE_KEY } from "../context-keys.js";

/**
 * Variant URL routes — returns presigned GET URLs for file variants.
 *
 * `GET /files/:fileId/:variantName/url` — returns a time-limited presigned URL
 * that browsers can use directly in `<img src>` or `fetch()`. The presigned URL
 * points at the public S3 endpoint (e.g., cloudflared tunnel to MinIO).
 *
 * Access control: JWT required (middleware pipeline) + read resolution check
 * (jwt_owner, jwt_org, authenticated, or callback — per context key config).
 * The presigned URL itself is public for its lifetime — the access gate is here.
 */
export function createVariantUrlRoutes(contextKeyConfigs: ContextKeyConfigMap): Hono {
  const app = new Hono();

  app.get("/files/:fileId/:variantName/url", async (c) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const scope = (c as any).get(SCOPE_KEY) as ServiceScope;
    const { fileId, variantName } = c.req.param();

    // Look up file metadata
    const metadataHandler = scope.resolve(IGetFileMetadataKey);
    const metaResult = await metadataHandler.handleAsync({ fileId });

    if (!metaResult.success || !metaResult.data) {
      return c.json(
        {
          success: false,
          statusCode: metaResult.statusCode,
          messages: metaResult.messages,
          data: null,
        },
        metaResult.statusCode as ContentfulStatusCode,
      );
    }

    const file = metaResult.data.file;

    // Only serve ready files
    if (file.status !== "ready") {
      return c.json(
        {
          success: false,
          statusCode: 404,
          messages: ["File is not ready for download."],
          data: null,
        },
        404 as ContentfulStatusCode,
      );
    }

    // Check read access via context key resolution strategy
    const ckConfig = contextKeyConfigs.get(file.contextKey);
    if (!ckConfig) {
      return c.json(
        { success: false, statusCode: 403, messages: ["Unknown context key."], data: null },
        403 as ContentfulStatusCode,
      );
    }

    const accessHandler = scope.resolve(IResolveFileAccessKey);
    const accessResult = await accessHandler.handleAsync({
      config: ckConfig,
      action: "read",
      relatedEntityId: file.relatedEntityId,
    });

    if (!accessResult.success) {
      return c.json(
        {
          success: false,
          statusCode: accessResult.statusCode,
          messages: accessResult.messages,
          data: null,
        },
        accessResult.statusCode as ContentfulStatusCode,
      );
    }

    // Verify the requested variant exists
    const variant = file.variants?.find((v) => v.size === variantName);
    if (!variant) {
      return c.json(
        { success: false, statusCode: 404, messages: ["Variant not found."], data: null },
        404 as ContentfulStatusCode,
      );
    }

    // Build storage key and generate presigned GET URL
    const storageKey = buildVariantStorageKey(
      {
        id: file.id,
        contextKey: file.contextKey,
        relatedEntityId: file.relatedEntityId,
      },
      variantName,
      variant.contentType,
    );

    const presignHandler = scope.resolve(IPresignGetUrlKey);
    const presignResult = await presignHandler.handleAsync({ key: storageKey });

    if (!presignResult.success || !presignResult.data) {
      return c.json(
        {
          success: false,
          statusCode: presignResult.statusCode,
          messages: presignResult.messages,
          data: null,
        },
        presignResult.statusCode as ContentfulStatusCode,
      );
    }

    return c.json(
      {
        success: true,
        statusCode: 200,
        messages: [],
        data: { url: presignResult.data.url },
      },
      200 as ContentfulStatusCode,
    );
  });

  return app;
}

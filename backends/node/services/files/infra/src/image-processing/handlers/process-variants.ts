import sharp from "sharp";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  ProcessVariantsInput as I,
  ProcessVariantsOutput as O,
  ProcessedVariant,
  IProcessVariants,
} from "@d2/files-app";

/**
 * Image processing provider using sharp.
 *
 * For each variant config with a maxDimension, resizes the image
 * to fit within that dimension (preserving aspect ratio, no enlargement)
 * and converts to WebP.
 *
 * SVG content is returned as-is for all variants (no resize needed).
 */
export class ProcessVariants extends BaseHandler<I, O> implements IProcessVariants {
  constructor(context: IHandlerContext) {
    super(context);
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true, suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const isSvg = input.contentType === "image/svg+xml";
    const variants: ProcessedVariant[] = [];

    for (const variantConfig of input.variants) {
      if (isSvg) {
        // SVG: return original buffer for all variants
        variants.push({
          size: variantConfig.name,
          buffer: input.buffer,
          width: 0,
          height: 0,
          sizeBytes: input.buffer.length,
          contentType: "image/svg+xml",
        });
        continue;
      }

      const maxDim = variantConfig.maxDimension;
      const result = await sharp(input.buffer)
        .resize({
          width: maxDim,
          height: maxDim,
          fit: "inside",
          withoutEnlargement: true,
        })
        .webp()
        .toBuffer({ resolveWithObject: true });

      variants.push({
        size: variantConfig.name,
        buffer: result.data,
        width: result.info.width,
        height: result.info.height,
        sizeBytes: result.info.size,
        contentType: "image/webp",
      });
    }

    return D2Result.ok({ data: { variants } });
  }
}

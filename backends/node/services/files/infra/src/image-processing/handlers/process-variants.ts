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
 * For each variant config with a maxDimension, resizes the image to fit
 * within that dimension (preserving aspect ratio, no enlargement) and
 * converts to WebP.
 *
 * SVG is not supported: it's excluded from `ALLOWED_CONTENT_TYPES.image`
 * because SVGs can embed `<script>` and would execute in the storage origin
 * via presigned GET URLs. UploadFile rejects unsupported content types
 * before this handler ever runs, so any SVG reaching here is a contract
 * violation.
 */
export class ProcessVariants extends BaseHandler<I, O> implements IProcessVariants {
  constructor(context: IHandlerContext) {
    super(context);
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true, suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const variants: ProcessedVariant[] = [];

    for (const variantConfig of input.variants) {
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

/**
 * Content category — groups MIME types for per-purpose validation.
 *
 * - `image` — raster/vector images (jpeg, png, gif, webp, svg, avif)
 * - `document` — structured documents (pdf, docx, xlsx, txt, csv)
 * - `video` — video files (mp4, webm, mov)
 * - `audio` — audio files (mpeg, ogg, wav, webm)
 */

export const CONTENT_CATEGORIES = ["image", "document", "video", "audio"] as const;

export type ContentCategory = (typeof CONTENT_CATEGORIES)[number];

export function isValidContentCategory(value: unknown): value is ContentCategory {
  return typeof value === "string" && CONTENT_CATEGORIES.includes(value as ContentCategory);
}

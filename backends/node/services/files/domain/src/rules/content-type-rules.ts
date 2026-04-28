import type { ContentCategory } from "../enums/content-category.js";
import { CONTENT_CATEGORIES } from "../enums/content-category.js";
import { ALLOWED_CONTENT_TYPES } from "../constants/files-constants.js";

/**
 * Resolves a MIME content type to its content category.
 *
 * @returns The matching category, or undefined if the MIME type is unknown
 */
export function resolveContentCategory(contentType: string): ContentCategory | undefined {
  for (const category of CONTENT_CATEGORIES) {
    if (ALLOWED_CONTENT_TYPES[category].includes(contentType)) {
      return category;
    }
  }
  return undefined;
}

/**
 * Checks whether a MIME content type is allowed within the given categories.
 *
 * @param contentType - MIME type to check (e.g., "image/jpeg")
 * @param allowedCategories - Categories to check against
 * @returns true if the content type belongs to one of the allowed categories
 */
export function isContentTypeAllowed(
  contentType: string,
  allowedCategories: readonly ContentCategory[],
): boolean {
  const category = resolveContentCategory(contentType);
  if (!category) return false;
  return allowedCategories.includes(category);
}

/**
 * Returns a flat array of all MIME types for the given categories.
 */
export function getAllowedContentTypes(categories: readonly ContentCategory[]): string[] {
  const result: string[] = [];
  for (const category of categories) {
    result.push(...ALLOWED_CONTENT_TYPES[category]);
  }
  return result;
}

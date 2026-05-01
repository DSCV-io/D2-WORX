import type { RequestHandler } from "./$types";
import { locales, baseLocale } from "$lib/paraglide/runtime.js";

/** Public pages that should appear in the sitemap. */
const publicPaths = [
  { path: "/", changefreq: "weekly", priority: "1.0" },
  { path: "/sign-in", changefreq: "monthly", priority: "0.8" },
  { path: "/sign-up", changefreq: "monthly", priority: "0.8" },
] as const;

function localizedPath(path: string, locale: string): string {
  if (locale === baseLocale) return path;
  return `/${locale}${path}`;
}

export const GET: RequestHandler = ({ url }) => {
  const origin = url.origin;

  const urls = publicPaths
    .map((p) => {
      const alternates = locales
        .map(
          (locale) =>
            `    <xhtml:link rel="alternate" hreflang="${locale}" href="${origin}${localizedPath(p.path, locale)}" />`,
        )
        .concat([
          `    <xhtml:link rel="alternate" hreflang="x-default" href="${origin}${p.path}" />`,
        ])
        .join("\n");

      return `  <url>
    <loc>${origin}${p.path}</loc>
    <changefreq>${p.changefreq}</changefreq>
    <priority>${p.priority}</priority>
${alternates}
  </url>`;
    })
    .join("\n");

  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
        xmlns:xhtml="http://www.w3.org/1999/xhtml">
${urls}
</urlset>`;

  return new Response(xml, {
    headers: {
      "Content-Type": "application/xml",
      "Cache-Control": "max-age=3600",
    },
  });
};

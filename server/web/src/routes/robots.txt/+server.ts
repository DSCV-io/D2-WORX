import type { RequestHandler } from "./$types";

export const GET: RequestHandler = ({ url }) => {
  const body = [
    "User-agent: *",
    "Disallow: /debug/",
    "Disallow: /api/",
    "",
    `Sitemap: ${url.origin}/sitemap.xml`,
  ].join("\n");

  return new Response(body, {
    headers: {
      "Content-Type": "text/plain",
      "Cache-Control": "max-age=86400",
    },
  });
};

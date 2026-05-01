import { describe, it, expect } from "vitest";
import { GET as getRobotsTxt } from "./robots.txt/+server";
import { GET as getSitemapXml } from "./sitemap.xml/+server";

// ---------------------------------------------------------------------------
// helpers
// ---------------------------------------------------------------------------

function callRobotsTxt(origin = "https://example.com") {
  const url = new URL(`${origin}/robots.txt`);
  return getRobotsTxt({ url } as any) as Response;
}

function callSitemapXml(origin = "https://example.com") {
  const url = new URL(`${origin}/sitemap.xml`);
  return getSitemapXml({ url } as any) as Response;
}

// ---------------------------------------------------------------------------
// robots.txt
// ---------------------------------------------------------------------------

describe("robots.txt", () => {
  it("returns 200 with Content-Type text/plain", async () => {
    const res = callRobotsTxt();

    expect(res.status).toBe(200);
    expect(res.headers.get("Content-Type")).toBe("text/plain");
  });

  it("returns Cache-Control max-age=86400 (24 hours)", async () => {
    const res = callRobotsTxt();

    expect(res.headers.get("Cache-Control")).toBe("max-age=86400");
  });

  it('contains "User-agent: *"', async () => {
    const body = await callRobotsTxt().text();

    expect(body).toContain("User-agent: *");
  });

  it("disallows /debug/ and /api/", async () => {
    const body = await callRobotsTxt().text();

    expect(body).toContain("Disallow: /debug/");
    expect(body).toContain("Disallow: /api/");
  });

  it("does NOT disallow public paths (/, /sign-in, /sign-up)", async () => {
    const body = await callRobotsTxt().text();
    const disallowLines = body.split("\n").filter((l: string) => l.startsWith("Disallow:"));

    for (const line of disallowLines) {
      const path = line.replace("Disallow:", "").trim();
      expect(path).not.toBe("/");
      expect(path).not.toBe("/sign-in");
      expect(path).not.toBe("/sign-up");
    }
  });

  it("contains a Sitemap directive with the correct origin", async () => {
    const body = await callRobotsTxt().text();

    expect(body).toContain("Sitemap: https://example.com/sitemap.xml");
  });

  it("uses the request url.origin for the Sitemap directive", async () => {
    const body = await callRobotsTxt("https://d2-worx.dev").text();

    expect(body).toContain("Sitemap: https://d2-worx.dev/sitemap.xml");
    expect(body).not.toContain("example.com");
  });
});

// ---------------------------------------------------------------------------
// sitemap.xml
// ---------------------------------------------------------------------------

describe("sitemap.xml", () => {
  it("returns 200 with Content-Type application/xml", async () => {
    const res = callSitemapXml();

    expect(res.status).toBe(200);
    expect(res.headers.get("Content-Type")).toBe("application/xml");
  });

  it("returns Cache-Control max-age=3600 (1 hour)", async () => {
    const res = callSitemapXml();

    expect(res.headers.get("Cache-Control")).toBe("max-age=3600");
  });

  it("contains a valid XML declaration", async () => {
    const body = await callSitemapXml().text();

    expect(body).toMatch(/^<\?xml version="1\.0" encoding="UTF-8"\?>/);
  });

  it("contains urlset with sitemap and xhtml namespaces", async () => {
    const body = await callSitemapXml().text();

    expect(body).toContain('xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"');
    expect(body).toContain('xmlns:xhtml="http://www.w3.org/1999/xhtml"');
  });

  it("contains all 3 public paths (/, /sign-in, /sign-up)", async () => {
    const body = await callSitemapXml().text();

    expect(body).toContain("<loc>https://example.com/</loc>");
    expect(body).toContain("<loc>https://example.com/sign-in</loc>");
    expect(body).toContain("<loc>https://example.com/sign-up</loc>");
  });

  it("root path has priority 1.0 and auth paths have priority 0.8", async () => {
    const body = await callSitemapXml().text();

    // Extract each <url> block and verify priorities
    const urlBlocks = body.split("<url>").slice(1); // skip content before first <url>

    // First block is root (/)
    expect(urlBlocks[0]).toContain("<loc>https://example.com/</loc>");
    expect(urlBlocks[0]).toContain("<priority>1.0</priority>");

    // Second block is /sign-in
    expect(urlBlocks[1]).toContain("<loc>https://example.com/sign-in</loc>");
    expect(urlBlocks[1]).toContain("<priority>0.8</priority>");

    // Third block is /sign-up
    expect(urlBlocks[2]).toContain("<loc>https://example.com/sign-up</loc>");
    expect(urlBlocks[2]).toContain("<priority>0.8</priority>");
  });

  it("each URL has hreflang alternates for all locales", async () => {
    const body = await callSitemapXml().text();
    const expectedLocales = [
      "en-US",
      "en-CA",
      "en-GB",
      "fr-FR",
      "fr-CA",
      "es-ES",
      "es-MX",
      "de-DE",
      "it-IT",
      "ja-JP",
    ];

    for (const locale of expectedLocales) {
      expect(body).toContain(`hreflang="${locale}"`);
    }
  });

  it("contains x-default hreflang alternate for each URL", async () => {
    const body = await callSitemapXml().text();

    // There should be one x-default per public path (3 total)
    const xDefaultMatches = body.match(/hreflang="x-default"/g);
    expect(xDefaultMatches).not.toBeNull();
    expect(xDefaultMatches!.length).toBe(3);
  });

  it("base locale paths are unprefixed, non-base locales are prefixed", async () => {
    const body = await callSitemapXml().text();

    // Base locale (en-US) — root path should be unprefixed
    expect(body).toContain('hreflang="en-US" href="https://example.com/"');

    // Non-base locale (es-ES) — root path should be prefixed with /es-ES
    expect(body).toContain('hreflang="es-ES" href="https://example.com/es-ES/"');

    // Non-base locale (ja-JP) — /sign-in should be /ja-JP/sign-in
    expect(body).toContain('hreflang="ja-JP" href="https://example.com/ja-JP/sign-in"');

    // x-default uses the unprefixed path (same as base locale)
    expect(body).toContain('hreflang="x-default" href="https://example.com/"');
    expect(body).toContain('hreflang="x-default" href="https://example.com/sign-in"');
    expect(body).toContain('hreflang="x-default" href="https://example.com/sign-up"');
  });

  it("does NOT contain private/internal paths", async () => {
    const body = await callSitemapXml().text();

    expect(body).not.toContain("/debug/");
    expect(body).not.toContain("/api/");
    expect(body).not.toContain("/dashboard");
    expect(body).not.toContain("/settings");
    expect(body).not.toContain("/profile");
  });

  it("uses url.origin from the request for all URLs", async () => {
    const body = await callSitemapXml("https://d2-worx.dev").text();

    expect(body).toContain("<loc>https://d2-worx.dev/</loc>");
    expect(body).toContain("<loc>https://d2-worx.dev/sign-in</loc>");
    expect(body).toContain("<loc>https://d2-worx.dev/sign-up</loc>");
    expect(body).toContain('href="https://d2-worx.dev/es-ES/"');
    expect(body).not.toContain("example.com");
  });

  it("includes correct changefreq values", async () => {
    const body = await callSitemapXml().text();
    const urlBlocks = body.split("<url>").slice(1);

    // Root is weekly
    expect(urlBlocks[0]).toContain("<changefreq>weekly</changefreq>");

    // Auth pages are monthly
    expect(urlBlocks[1]).toContain("<changefreq>monthly</changefreq>");
    expect(urlBlocks[2]).toContain("<changefreq>monthly</changefreq>");
  });
});

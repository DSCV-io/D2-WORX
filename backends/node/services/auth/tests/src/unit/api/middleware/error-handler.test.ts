import { describe, it, expect } from "vitest";
import { Hono } from "hono";
import { createErrorHandler } from "@d2/auth-api";
import { AuthValidationError } from "@d2/auth-domain";
import { TK } from "@d2/i18n";
import { createLogger } from "@d2/logging";

const silentLogger = createLogger({ level: "silent" as never });

function createApp() {
  const app = new Hono();
  app.onError(createErrorHandler(silentLogger));
  return app;
}

describe("Error handler", () => {
  it("should return 400 with TK key for AuthValidationError", async () => {
    const app = createApp();
    app.get("/test", () => {
      throw new AuthValidationError("User", "email", "bad@", "invalid format");
    });

    const res = await app.request("/test");
    expect(res.status).toBe(400);

    const body = await res.json();
    expect(body.success).toBe(false);
    // Should contain the TK validation key — translation happens in middleware
    expect(body.messages[0]).toBe(TK.common.errors.VALIDATION_FAILED);
    // Should NOT contain the raw invalid value or full error message
    expect(body.messages[0]).not.toContain("bad@");
  });

  it("should return 500 with TK key for unknown errors", async () => {
    const app = createApp();
    app.get("/test", () => {
      throw new Error("SQL injection attempt SELECT * FROM users");
    });

    const res = await app.request("/test");
    expect(res.status).toBe(500);

    const body = await res.json();
    expect(body.success).toBe(false);
    // Must NOT leak the actual error message
    expect(body.messages[0]).not.toContain("SQL");
    expect(body.messages[0]).not.toContain("SELECT");
    // Should contain the TK key — translation happens in middleware
    expect(body.messages[0]).toBe(TK.common.errors.UNKNOWN);
  });

  it("should not leak file paths in 5xx errors", async () => {
    const app = createApp();
    app.get("/test", () => {
      throw new Error("ENOENT: /home/deploy/secrets/credentials.json");
    });

    const res = await app.request("/test");
    const body = await res.json();
    expect(JSON.stringify(body)).not.toContain("deploy");
    expect(JSON.stringify(body)).not.toContain("secrets");
    expect(JSON.stringify(body)).not.toContain("credentials");
  });

  it("should not leak stack traces", async () => {
    const app = createApp();
    app.get("/test", () => {
      const err = new Error("internal details");
      err.stack = "Error: internal details\n    at /app/node_modules/pg/query.js:123";
      throw err;
    });

    const res = await app.request("/test");
    const body = await res.json();
    const bodyStr = JSON.stringify(body);
    expect(bodyStr).not.toContain("node_modules");
    expect(bodyStr).not.toContain("internal details");
    expect(bodyStr).not.toContain("query.js");
  });

  it("should return safe message for 403 errors", async () => {
    const app = createApp();
    app.get("/test", () => {
      const err = new Error("User admin@corp.com tried to access /admin/delete-all");
      (err as Error & { status: number }).status = 403;
      throw err;
    });

    const res = await app.request("/test");
    expect(res.status).toBe(403);

    const body = await res.json();
    expect(body.messages[0]).toBe(TK.common.errors.FORBIDDEN);
    expect(JSON.stringify(body)).not.toContain("admin@corp.com");
    expect(JSON.stringify(body)).not.toContain("delete-all");
  });

  it("should return safe message for 404 errors", async () => {
    const app = createApp();
    app.get("/test", () => {
      const err = new Error("Could not find user with id=secret-uuid-123");
      (err as Error & { status: number }).status = 404;
      throw err;
    });

    const res = await app.request("/test");
    expect(res.status).toBe(404);

    const body = await res.json();
    expect(body.messages[0]).toBe(TK.common.errors.NOT_FOUND);
    expect(JSON.stringify(body)).not.toContain("secret-uuid-123");
  });

  it("should return safe message for 429 errors", async () => {
    const app = createApp();
    app.get("/test", () => {
      const err = new Error("IP 1.2.3.4 exceeded rate limit");
      (err as Error & { status: number }).status = 429;
      throw err;
    });

    const res = await app.request("/test");
    expect(res.status).toBe(429);

    const body = await res.json();
    expect(body.messages[0]).toBe(TK.common.errors.TOO_MANY_REQUESTS);
    expect(JSON.stringify(body)).not.toContain("1.2.3.4");
  });

  it("should always return D2Result shape", async () => {
    const app = createApp();
    app.get("/test", () => {
      throw new Error("boom");
    });

    const res = await app.request("/test");
    const body = await res.json();
    expect(body).toHaveProperty("success", false);
    expect(body).toHaveProperty("statusCode");
    expect(body).toHaveProperty("messages");
    expect(Array.isArray(body.messages)).toBe(true);
  });
});

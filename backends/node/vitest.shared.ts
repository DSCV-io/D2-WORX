import { resolve } from "node:path";
import { defineConfig } from "vitest/config";

const shared = resolve(import.meta.dirname, "shared");
const impl = resolve(shared, "implementations");

/**
 * Shared Vitest configuration inherited by all test projects.
 *
 * resolve.alias maps @d2/* workspace imports to their TypeScript source
 * so V8 coverage can instrument the actual source files instead of
 * compiled dist/ output.
 */
export default defineConfig({
  resolve: {
    alias: {
      "@d2/result": resolve(shared, "result/src/index.ts"),
      "@d2/utilities": resolve(shared, "utilities/src/index.ts"),
      "@d2/protos": resolve(shared, "protos/src/index.ts"),
      "@d2/logging": resolve(shared, "logging/src/index.ts"),
      "@d2/service-defaults/config": resolve(shared, "service-defaults/src/config/index.ts"),
      "@d2/service-defaults/grpc": resolve(shared, "service-defaults/src/grpc/index.ts"),
      "@d2/service-defaults": resolve(shared, "service-defaults/src/index.ts"),
      "@d2/handler": resolve(shared, "handler/src/index.ts"),
      "@d2/interfaces": resolve(shared, "interfaces/src/index.ts"),
      "@d2/result-extensions": resolve(shared, "result-extensions/src/index.ts"),
      "@d2/testing": resolve(shared, "testing/src/index.ts"),
      "@d2/cache-memory": resolve(impl, "caching/in-memory/default/src/index.ts"),
      "@d2/cache-redis": resolve(impl, "caching/distributed/redis/src/index.ts"),
      "@d2/messaging": resolve(shared, "messaging/src/index.ts"),
      "@d2/i18n": resolve(shared, "i18n/src/index.ts"),
      "@d2/geo-client": resolve(import.meta.dirname, "services/geo/geo-client/src/index.ts"),
      "@d2/request-enrichment": resolve(impl, "middleware/request-enrichment/default/src/index.ts"),
      "@d2/ratelimit": resolve(impl, "middleware/ratelimit/default/src/index.ts"),
      "@d2/auth-bff-client": resolve(import.meta.dirname, "services/auth/bff-client/src/index.ts"),
      "@d2/jwt-auth": resolve(impl, "middleware/jwt-auth/default/src/index.ts"),
      "@d2/auth-policy": resolve(impl, "middleware/auth-policy/default/src/index.ts"),
      "@d2/auth-domain": resolve(import.meta.dirname, "services/auth/domain/src/index.ts"),
      "@d2/auth-infra": resolve(import.meta.dirname, "services/auth/infra/src/index.ts"),
      "@d2/auth-app": resolve(import.meta.dirname, "services/auth/app/src/index.ts"),
      "@d2/auth-api": resolve(import.meta.dirname, "services/auth/api/src/index.ts"),
      "@d2/comms-domain": resolve(import.meta.dirname, "services/comms/domain/src/index.ts"),
      "@d2/comms-app": resolve(import.meta.dirname, "services/comms/app/src/index.ts"),
      "@d2/comms-infra": resolve(import.meta.dirname, "services/comms/infra/src/index.ts"),
      "@d2/comms-api": resolve(import.meta.dirname, "services/comms/api/src/index.ts"),
      "@d2/files-domain": resolve(import.meta.dirname, "services/files/domain/src/index.ts"),
      "@d2/files-app": resolve(import.meta.dirname, "services/files/app/src/index.ts"),
      "@d2/files-infra": resolve(import.meta.dirname, "services/files/infra/src/index.ts"),
      "@d2/dkron-mgr": resolve(import.meta.dirname, "services/dkron-mgr/src/index.ts"),
    },
  },
  test: {
    globals: true,
    environment: "node",
    testTimeout: 10_000,
    hookTimeout: 60_000,
    clearMocks: true,
    restoreMocks: true,
    include: ["**/*.test.ts", "**/*.spec.ts"],
  },
});

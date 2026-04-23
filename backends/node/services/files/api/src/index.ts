export { createFilesApp } from "./composition-root.js";
export type { FilesServiceConfig } from "./composition-root.js";

// Exported for security-conformance tests that exercise the gateways' fail-
// closed posture without spinning up the full composition root.
export { buildGrpcServer } from "./setup/grpc-server-setup.js";
export type { GrpcServerOptions } from "./setup/grpc-server-setup.js";
export { buildHonoApp } from "./setup/hono-app-setup.js";
export type { HonoAppOptions } from "./setup/hono-app-setup.js";

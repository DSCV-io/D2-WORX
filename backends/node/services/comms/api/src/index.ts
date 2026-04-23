export { createCommsService } from "./composition-root.js";
export type { CommsServiceConfig } from "./composition-root.js";

// Exported for testing
export { createCommsGrpcService } from "./services/comms-grpc-service.js";
export { withApiKeyAuth } from "@d2/service-defaults/grpc";
// Exported for security-conformance tests that exercise the gRPC server
// builder's fail-closed posture without spinning up the full composition root.
export { buildGrpcServer } from "./setup/grpc-server-setup.js";
export type { CommsGrpcServerOptions } from "./setup/grpc-server-setup.js";
export { channelPreferenceToProto } from "./mappers/channel-preference-mapper.js";
export { deliveryRequestToProto, deliveryAttemptToProto } from "./mappers/delivery-mapper.js";

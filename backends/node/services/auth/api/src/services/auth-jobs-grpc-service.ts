import type { AuthJobServiceServer } from "@d2/protos";
import type { ServiceProvider } from "@d2/di";
import { handleJobRpc } from "@d2/service-defaults/grpc";
import {
  IRunSessionPurgeKey,
  IRunSignInEventPurgeKey,
  IRunInvitationCleanupKey,
  IRunEmulationConsentCleanupKey,
  ICleanupDeletedUsersKey,
} from "@d2/auth-app";

/**
 * Creates the AuthJobServiceServer implementation.
 * Each RPC delegates to {@link handleJobRpc} which manages scope, tracing, and error handling.
 */
export function createAuthJobsGrpcService(provider: ServiceProvider): AuthJobServiceServer {
  return {
    purgeExpiredSessions: (call, callback) =>
      handleJobRpc(provider, call, callback, IRunSessionPurgeKey, "purge-expired-sessions"),

    purgeSignInEvents: (call, callback) =>
      handleJobRpc(provider, call, callback, IRunSignInEventPurgeKey, "purge-sign-in-events"),

    cleanupExpiredInvitations: (call, callback) =>
      handleJobRpc(
        provider,
        call,
        callback,
        IRunInvitationCleanupKey,
        "cleanup-expired-invitations",
      ),

    cleanupExpiredEmulationConsents: (call, callback) =>
      handleJobRpc(
        provider,
        call,
        callback,
        IRunEmulationConsentCleanupKey,
        "cleanup-expired-emulation-consents",
      ),

    cleanupDeletedUsers: (call, callback) =>
      handleJobRpc(provider, call, callback, ICleanupDeletedUsersKey, "cleanup-deleted-users"),
  };
}

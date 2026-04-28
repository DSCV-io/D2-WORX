import { describe, it, expect, vi } from "vitest";
import * as grpc from "@grpc/grpc-js";
import { D2Result } from "@d2/result";
import { createCommsGrpcService } from "@d2/comms-api";
import type { ChannelPreference, DeliveryRequest, DeliveryAttempt } from "@d2/comms-domain";

/** Creates a mock ServiceProvider that returns controllable scopes. */
function createMockProvider(handlerMocks: Record<symbol, unknown> = {}) {
  const instances = new Map<unknown, unknown>();

  const scope = {
    setInstance: vi.fn((key: unknown, value: unknown) => {
      instances.set(key, value);
    }),
    resolve: vi.fn((key: unknown) => {
      if (handlerMocks[key as symbol]) return handlerMocks[key as symbol];
      throw new Error(`Key not registered: ${String(key)}`);
    }),
    dispose: vi.fn(),
  };

  return {
    createScope: vi.fn(() => scope),
    resolve: vi.fn(),
    scope,
  };
}

function okHandler<T>(data: T) {
  return { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data })) };
}

function failHandler(statusCode = 500) {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.fail({ messages: ["Error"], statusCode })),
  };
}

function throwHandler(msg = "Unexpected error") {
  return { handleAsync: vi.fn().mockRejectedValue(new Error(msg)) };
}

const samplePref: ChannelPreference = {
  id: "pref-1",
  contactId: "contact-1",
  emailEnabled: true,
  smsEnabled: false,
  createdAt: new Date("2026-01-01"),
  updatedAt: new Date("2026-01-01"),
};

const sampleRequest: DeliveryRequest = {
  id: "req-1",
  messageId: "msg-1",
  correlationId: "corr-1",
  recipientContactId: "contact-1",
  createdAt: new Date("2026-01-01"),
};

const sampleAttempt: DeliveryAttempt = {
  id: "att-1",
  requestId: "req-1",
  channel: "email",
  recipientAddress: "user@example.com",
  status: "sent",
  providerMessageId: "resend-123",
  attemptNumber: 1,
  createdAt: new Date("2026-01-01"),
};

// Import service keys indirectly — we need their symbols for mock routing
// Since we can't import the actual symbols in tests easily, we use the
// provider.scope.resolve mock to return handlers by call order instead.

describe("createCommsGrpcService", () => {
  describe("getChannelPreference", () => {
    it("should return preference on success", async () => {
      const handler = okHandler({ pref: samplePref });
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.getChannelPreference({ request: { contactId: "contact-1" } } as any, callback);

      expect(callback).toHaveBeenCalledOnce();
      const [err, response] = callback.mock.calls[0];
      expect(err).toBeNull();
      expect(response.data.id).toBe("pref-1");
      expect(response.data.emailEnabled).toBe(true);
    });

    it("should return NOT_FOUND when no preference exists", async () => {
      const handler = okHandler({ pref: null });
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.getChannelPreference({ request: { contactId: "contact-1" } } as any, callback);

      expect(callback).toHaveBeenCalledOnce();
      const [, response] = callback.mock.calls[0];
      expect(response.result.errorCode).toBe("NOT_FOUND");
    });

    it("should return INTERNAL error when handler throws", async () => {
      const handler = throwHandler("DB connection failed");
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.getChannelPreference({ request: { contactId: "contact-1" } } as any, callback);

      expect(callback).toHaveBeenCalledOnce();
      const [err] = callback.mock.calls[0];
      expect(err.code).toBe(grpc.status.INTERNAL);
      expect(err.message).toBe("DB connection failed");
    });
  });

  describe("setChannelPreference", () => {
    it("should return preference on success", async () => {
      const handler = okHandler({ pref: samplePref });
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.setChannelPreference(
        {
          request: {
            contactId: "contact-1",
            emailEnabled: true,
            smsEnabled: false,
          },
        } as any,
        callback,
      );

      expect(callback).toHaveBeenCalledOnce();
      const [err, response] = callback.mock.calls[0];
      expect(err).toBeNull();
      expect(response.data.id).toBe("pref-1");
    });

    it("should return error result when handler fails", async () => {
      const handler = failHandler(400);
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.setChannelPreference(
        {
          request: {
            contactId: "contact-1",
            emailEnabled: true,
            smsEnabled: false,
          },
        } as any,
        callback,
      );

      expect(callback).toHaveBeenCalledOnce();
      const [, response] = callback.mock.calls[0];
      expect(response.data).toBeUndefined();
    });

    it("should return INTERNAL error when handler throws", async () => {
      const handler = throwHandler();
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.setChannelPreference(
        {
          request: {
            contactId: "contact-1",
            emailEnabled: true,
            smsEnabled: false,
          },
        } as any,
        callback,
      );

      const [err] = callback.mock.calls[0];
      expect(err.code).toBe(grpc.status.INTERNAL);
    });
  });

  describe("getDeliveryStatus", () => {
    it("should return request and attempts on success", async () => {
      const findRequestHandler = okHandler({ request: sampleRequest });
      const findAttemptsHandler = okHandler({ attempts: [sampleAttempt] });
      const provider = createMockProvider();
      // First resolve → findRequestById, second → findAttemptsByRequestId
      provider.scope.resolve
        .mockReturnValueOnce(findRequestHandler)
        .mockReturnValueOnce(findAttemptsHandler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.getDeliveryStatus({ request: { deliveryRequestId: "req-1" } } as any, callback);

      expect(callback).toHaveBeenCalledOnce();
      const [err, response] = callback.mock.calls[0];
      expect(err).toBeNull();
      expect(response.request.id).toBe("req-1");
      expect(response.attempts).toHaveLength(1);
      expect(response.attempts[0].channel).toBe("email");
    });

    it("should return NOT_FOUND when request does not exist", async () => {
      const findRequestHandler = okHandler({ request: null });
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(findRequestHandler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.getDeliveryStatus(
        { request: { deliveryRequestId: "nonexistent" } } as any,
        callback,
      );

      const [, response] = callback.mock.calls[0];
      expect(response.result.errorCode).toBe("NOT_FOUND");
      expect(response.request).toBeUndefined();
    });

    it("should return INTERNAL error when handler throws", async () => {
      const handler = throwHandler("DB error");
      const provider = createMockProvider();
      provider.scope.resolve.mockReturnValue(handler);

      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      await service.getDeliveryStatus({ request: { deliveryRequestId: "req-1" } } as any, callback);

      const [err] = callback.mock.calls[0];
      expect(err.code).toBe(grpc.status.INTERNAL);
    });
  });

  describe("stub RPCs", () => {
    const stubNames = [
      "getNotifications",
      "markNotificationsRead",
      "createThread",
      "getThread",
      "getThreads",
      "postMessage",
      "editMessage",
      "deleteMessage",
      "getThreadMessages",
      "addReaction",
      "removeReaction",
      "addParticipant",
      "removeParticipant",
    ] as const;

    it.each(stubNames)("%s should return UNIMPLEMENTED", (rpcName) => {
      const provider = createMockProvider();
      const service = createCommsGrpcService(provider as any);
      const callback = vi.fn();

      (service[rpcName] as any)({} as any, callback);

      expect(callback).toHaveBeenCalledOnce();
      const [err] = callback.mock.calls[0];
      expect(err.code).toBe(grpc.status.UNIMPLEMENTED);
    });
  });
});

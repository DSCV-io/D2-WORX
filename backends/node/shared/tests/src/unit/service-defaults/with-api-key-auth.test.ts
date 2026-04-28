import { describe, it, expect, vi } from "vitest";
import * as grpc from "@grpc/grpc-js";
import { withApiKeyAuth } from "@d2/service-defaults/grpc";
import { type ILogger } from "@d2/logging";

const stubLogger: ILogger = {
  info: vi.fn(),
  warn: vi.fn(),
  error: vi.fn(),
  debug: vi.fn(),
  child: vi.fn().mockReturnThis(),
} as unknown as ILogger;

const noopHandler: grpc.handleUnaryCall<unknown, unknown> = (_call, callback) => {
  callback(null, {});
};

describe("withApiKeyAuth", () => {
  describe("fail-closed at construction", () => {
    it("throws when validKeys is empty", () => {
      expect(() =>
        withApiKeyAuth(
          { method: noopHandler },
          { validKeys: new Set<string>(), logger: stubLogger },
        ),
      ).toThrow(/validKeys is empty/);
    });

    it("includes recovery guidance in the error message", () => {
      expect(() =>
        withApiKeyAuth(
          { method: noopHandler },
          { validKeys: new Set<string>(), logger: stubLogger },
        ),
      ).toThrow(/at least one key|intentionally disabled/);
    });

    it("does not throw when validKeys has at least one key", () => {
      expect(() =>
        withApiKeyAuth(
          { method: noopHandler },
          { validKeys: new Set(["valid-key-1"]), logger: stubLogger },
        ),
      ).not.toThrow();
    });
  });
});

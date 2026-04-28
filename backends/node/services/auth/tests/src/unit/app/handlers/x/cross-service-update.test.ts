import { describe, it, expect, vi, beforeEach } from "vitest";
import { HandlerContext, type IRequestContext } from "@d2/handler";
import { D2Result, HttpStatusCode } from "@d2/result";
import type { Complex } from "@d2/geo-client";
import type { ContactToCreateDTO } from "@d2/protos";
import { runCrossServiceUpdate } from "@d2/auth-app";

const VALID_USER_ID = "01234567-89ab-cdef-0123-456789abcdef";

function createTestContext() {
  const request: IRequestContext = {
    traceId: "trace-test",
    isAuthenticated: true,
    isTrustedService: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
  };
  const logger = {
    trace: vi.fn(),
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    fatal: vi.fn(),
    child: vi.fn(),
  };
  return {
    context: new HandlerContext(request, logger as never),
    logger,
  };
}

function makeContact(label: string): ContactToCreateDTO {
  return {
    contextKey: "auth_user",
    relatedEntityId: VALID_USER_ID,
    createdAt: new Date(),
    personalDetails: { firstName: label, lastName: label },
  } as ContactToCreateDTO;
}

function mockGeo(impl?: (input: { contacts: ContactToCreateDTO[] }) => Promise<unknown>) {
  return {
    handleAsync: vi.fn().mockImplementation(
      impl ??
        (async () =>
          D2Result.ok({
            data: { replacements: [{ newContact: { id: "geo-001" } }] },
          })),
    ),
  } as unknown as Complex.IUpdateContactsByExtKeysHandler;
}

describe("runCrossServiceUpdate (saga helper)", () => {
  let oldContact: ContactToCreateDTO;
  let newContact: ContactToCreateDTO;

  beforeEach(() => {
    oldContact = makeContact("OLD");
    newContact = makeContact("NEW");
  });

  // -----------------------------------------------------------------------
  // Happy path
  // -----------------------------------------------------------------------

  it("returns the auth result when both Geo + Auth succeed", async () => {
    const geo = mockGeo();
    const { context } = createTestContext();
    const authUpdate = vi.fn().mockResolvedValue(D2Result.ok({ data: { updated: true } }));

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate,
      operationLabel: "test.op",
      context,
    });

    expect(result.success).toBe(true);
    expect(result.data).toEqual({ updated: true });
    expect(geo.handleAsync).toHaveBeenCalledTimes(1);
    expect(geo.handleAsync).toHaveBeenCalledWith({ contacts: [newContact] });
    expect(authUpdate).toHaveBeenCalledTimes(1);
  });

  it("invokes onGeoSuccess callback after Geo succeeds, before auth", async () => {
    const geo = mockGeo();
    const { context } = createTestContext();
    const order: string[] = [];
    const onGeoSuccess = vi.fn(() => order.push("onGeoSuccess"));
    const authUpdate = vi.fn().mockImplementation(async () => {
      order.push("authUpdate");
      return D2Result.ok({ data: {} });
    });

    await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate,
      operationLabel: "test.op",
      context,
      onGeoSuccess,
    });

    expect(onGeoSuccess).toHaveBeenCalledTimes(1);
    expect(order).toEqual(["onGeoSuccess", "authUpdate"]);
  });

  // -----------------------------------------------------------------------
  // Geo failure → abort
  // -----------------------------------------------------------------------

  it("returns serviceUnavailable when Geo fails — auth is NOT called, no rollback", async () => {
    const geo = mockGeo(async () =>
      D2Result.fail({
        messages: ["geo internal error"],
        statusCode: HttpStatusCode.InternalServerError,
      }),
    );
    const { context, logger } = createTestContext();
    const authUpdate = vi.fn();

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate,
      operationLabel: "test.op",
      context,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(authUpdate).not.toHaveBeenCalled();
    expect(geo.handleAsync).toHaveBeenCalledTimes(1);
    expect(logger.error).toHaveBeenCalledWith(
      expect.stringContaining("Geo update failed"),
      expect.any(Object),
    );
  });

  // -----------------------------------------------------------------------
  // Auth failure → compensate via Geo rollback
  // -----------------------------------------------------------------------

  it("rolls Geo back to oldContact when auth fails, returns the auth failure", async () => {
    const geo = mockGeo();
    const { context, logger } = createTestContext();
    const authUpdate = vi
      .fn()
      .mockResolvedValue(D2Result.fail({ messages: ["bad"], statusCode: 500 }));

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate,
      operationLabel: "test.op",
      context,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(500);
    expect(geo.handleAsync).toHaveBeenCalledTimes(2);
    // Second call is the rollback — uses the OLD contact.
    expect(geo.handleAsync).toHaveBeenNthCalledWith(2, { contacts: [oldContact] });
    expect(logger.warn).toHaveBeenCalledWith(
      expect.stringContaining("Geo rolled back successfully"),
      expect.any(Object),
    );
    expect(logger.fatal).not.toHaveBeenCalled();
  });

  it("treats a thrown auth error as a failure and triggers rollback", async () => {
    const geo = mockGeo();
    const { context, logger } = createTestContext();
    const authUpdate = vi.fn().mockRejectedValue(new Error("DB connection died"));

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate,
      operationLabel: "test.op",
      context,
    });

    expect(result.success).toBe(false);
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(geo.handleAsync).toHaveBeenCalledTimes(2);
    expect(geo.handleAsync).toHaveBeenNthCalledWith(2, { contacts: [oldContact] });
    expect(logger.error).toHaveBeenCalledWith(
      expect.stringContaining("auth update threw"),
      expect.objectContaining({ error: "DB connection died" }),
    );
  });

  // -----------------------------------------------------------------------
  // CRITICAL — rollback failure
  // -----------------------------------------------------------------------

  it("logs FATAL when both auth update AND Geo rollback fail", async () => {
    let callCount = 0;
    const geo = {
      handleAsync: vi.fn().mockImplementation(async () => {
        callCount++;
        if (callCount === 1) {
          return D2Result.ok({ data: { replacements: [{ newContact: { id: "g1" } }] } });
        }
        // Rollback fails too — system inconsistency.
        return D2Result.fail({
          messages: ["rollback failed"],
          statusCode: HttpStatusCode.InternalServerError,
        });
      }),
    } as unknown as Complex.IUpdateContactsByExtKeysHandler;

    const { context, logger } = createTestContext();
    const authUpdate = vi
      .fn()
      .mockResolvedValue(D2Result.fail({ messages: ["auth bad"], statusCode: 500 }));

    const result = await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate,
      operationLabel: "test.op",
      context,
    });

    expect(result.success).toBe(false);
    // Returned error is the original auth failure, not the rollback one.
    expect(result.statusCode).toBe(500);
    expect(geo.handleAsync).toHaveBeenCalledTimes(2);
    expect(logger.fatal).toHaveBeenCalledTimes(1);
    expect(logger.fatal).toHaveBeenCalledWith(
      expect.stringContaining("CRITICAL"),
      expect.objectContaining({
        compensateStatusCode: HttpStatusCode.InternalServerError,
        authStatusCode: 500,
      }),
    );
    expect(logger.warn).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // operationLabel propagation
  // -----------------------------------------------------------------------

  it("includes operationLabel in all log messages", async () => {
    const geo = mockGeo(async () => D2Result.fail({ messages: ["x"], statusCode: 500 }));
    const { context, logger } = createTestContext();

    await runCrossServiceUpdate({
      oldContact,
      newContact,
      updateContactsByExtKeys: geo,
      authUpdate: vi.fn(),
      operationLabel: "my.unique.label",
      context,
    });

    const errorCall = logger.error.mock.calls[0];
    expect(errorCall[0]).toContain("my.unique.label");
  });
});

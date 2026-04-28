import { describe, it, expect, vi, beforeEach } from "vitest";
import { D2Result } from "@d2/result";
import { FILES_MESSAGING } from "@d2/files-domain";
import type { IHandlerContext, IRequestContext } from "@d2/handler";
import type { IMessagePublisher } from "@d2/messaging";
import type { FilesCommands, IPublishFileForProcessingHandler } from "@d2/files-app";
import { PublishFileForProcessing, IntakeFileUploaded, ProcessUploadedFile } from "@d2/files-infra";

// --- Helpers ---

function createTestContext(): IHandlerContext {
  const request: IRequestContext = {
    isAuthenticated: true,
    isOrgEmulating: null,
    isUserImpersonating: null,
    isTrustedService: null,
    userId: "user-123",
    targetOrgId: "org-456",
  };
  return {
    request,
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
      trace: vi.fn(),
      fatal: vi.fn(),
      child: vi.fn().mockReturnThis(),
    },
  } as unknown as IHandlerContext;
}

function createMockPublisher(): IMessagePublisher {
  return {
    send: vi.fn().mockResolvedValue(undefined),
    close: vi.fn().mockResolvedValue(undefined),
  };
}

function createMockIntakeFile(success = true): FilesCommands.IIntakeFileHandler {
  return {
    handleAsync: success
      ? vi.fn().mockResolvedValue(D2Result.ok({ data: { discarded: false, file: {} } }))
      : vi.fn().mockResolvedValue(D2Result.serviceUnavailable()),
  } as unknown as FilesCommands.IIntakeFileHandler;
}

function createMockPublishForProcessing(): IPublishFileForProcessingHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
  } as unknown as IPublishFileForProcessingHandler;
}

function createMockProcessFile(success = true): FilesCommands.IProcessFileHandler {
  return {
    handleAsync: success
      ? vi.fn().mockResolvedValue(D2Result.ok({ data: { file: {} } }))
      : vi.fn().mockResolvedValue(D2Result.serviceUnavailable()),
  } as unknown as FilesCommands.IProcessFileHandler;
}

// --- PublishFileForProcessing Tests ---

describe("PublishFileForProcessing", () => {
  let handler: PublishFileForProcessing;
  let publisher: IMessagePublisher;

  beforeEach(() => {
    publisher = createMockPublisher();
    handler = new PublishFileForProcessing(publisher, createTestContext());
  });

  it("should publish fileId to the processing queue and return ok", async () => {
    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result).toBeSuccess();
    expect(publisher.send).toHaveBeenCalledTimes(1);
  });

  it("should publish to correct exchange and routing key from FILES_MESSAGING", async () => {
    await handler.handleAsync({ fileId: "file-002" });

    expect(publisher.send).toHaveBeenCalledWith(
      {
        exchange: FILES_MESSAGING.EVENTS_EXCHANGE,
        routingKey: FILES_MESSAGING.PROCESSING_ROUTING_KEY,
      },
      { fileId: "file-002" },
    );
  });

  it("should publish message with correct shape", async () => {
    await handler.handleAsync({ fileId: "file-abc-123" });

    const sentMessage = vi.mocked(publisher.send).mock.calls[0]![1] as {
      fileId: string;
    };
    expect(sentMessage).toEqual({ fileId: "file-abc-123" });
  });

  it("should publish to files.events exchange", async () => {
    await handler.handleAsync({ fileId: "file-x" });

    const target = vi.mocked(publisher.send).mock.calls[0]![0] as {
      exchange: string;
    };
    expect(target.exchange).toBe("files.events");
  });

  it("should publish with file-process routing key", async () => {
    await handler.handleAsync({ fileId: "file-y" });

    const target = vi.mocked(publisher.send).mock.calls[0]![0] as {
      routingKey: string;
    };
    expect(target.routingKey).toBe("file-process");
  });
});

// --- IntakeFileUploaded Tests ---

describe("IntakeFileUploaded", () => {
  let handler: IntakeFileUploaded;
  let intakeFile: FilesCommands.IIntakeFileHandler;
  let publishForProcessing: IPublishFileForProcessingHandler;

  beforeEach(() => {
    intakeFile = createMockIntakeFile();
    publishForProcessing = createMockPublishForProcessing();
    handler = new IntakeFileUploaded(intakeFile, publishForProcessing, createTestContext());
  });

  it("should delegate to IntakeFile handler and return ok on success", async () => {
    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result).toBeSuccess();
    expect(intakeFile.handleAsync).toHaveBeenCalledWith({ fileId: "file-001" });
  });

  it("should pass correct fileId to delegate handler", async () => {
    await handler.handleAsync({ fileId: "file-xyz-789" });

    expect(intakeFile.handleAsync).toHaveBeenCalledWith({ fileId: "file-xyz-789" });
  });

  it("should propagate failure from IntakeFile handler", async () => {
    intakeFile = createMockIntakeFile(false);
    handler = new IntakeFileUploaded(intakeFile, publishForProcessing, createTestContext());

    const result = await handler.handleAsync({ fileId: "file-fail" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should propagate notFound from IntakeFile handler", async () => {
    vi.mocked(intakeFile.handleAsync).mockResolvedValue(D2Result.notFound());

    const result = await handler.handleAsync({ fileId: "nonexistent" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(404);
  });

  it("should call delegate handler exactly once", async () => {
    await handler.handleAsync({ fileId: "file-001" });

    expect(intakeFile.handleAsync).toHaveBeenCalledTimes(1);
  });
});

// --- ProcessUploadedFile Tests ---

describe("ProcessUploadedFile", () => {
  let handler: ProcessUploadedFile;
  let processFile: FilesCommands.IProcessFileHandler;

  beforeEach(() => {
    processFile = createMockProcessFile();
    handler = new ProcessUploadedFile(processFile, createTestContext());
  });

  it("should delegate to ProcessFile handler and return ok on success", async () => {
    const result = await handler.handleAsync({ fileId: "file-001" });

    expect(result).toBeSuccess();
    expect(processFile.handleAsync).toHaveBeenCalledWith({ fileId: "file-001" });
  });

  it("should pass correct fileId to delegate handler", async () => {
    await handler.handleAsync({ fileId: "file-abc-456" });

    expect(processFile.handleAsync).toHaveBeenCalledWith({ fileId: "file-abc-456" });
  });

  it("should propagate failure from ProcessFile handler", async () => {
    processFile = createMockProcessFile(false);
    handler = new ProcessUploadedFile(processFile, createTestContext());

    const result = await handler.handleAsync({ fileId: "file-fail" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(503);
  });

  it("should propagate notFound from ProcessFile handler", async () => {
    vi.mocked(processFile.handleAsync).mockResolvedValue(D2Result.notFound());

    const result = await handler.handleAsync({ fileId: "nonexistent" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(404);
  });

  it("should call delegate handler exactly once", async () => {
    await handler.handleAsync({ fileId: "file-001" });

    expect(processFile.handleAsync).toHaveBeenCalledTimes(1);
  });

  it("should propagate forbidden from ProcessFile handler", async () => {
    vi.mocked(processFile.handleAsync).mockResolvedValue(D2Result.forbidden());

    const result = await handler.handleAsync({ fileId: "file-no-access" });

    expect(result).toBeFailure();
    expect(result).toHaveStatusCode(403);
  });
});

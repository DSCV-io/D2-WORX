import { describe, it, expect, vi, beforeEach } from "vitest";
import type { IHandlerContext, IRequestContext } from "@d2/handler";
import type { VariantConfig } from "@d2/files-domain";

// --- Hoisted mocks ---

const { mockSharpFn } = vi.hoisted(() => ({
  mockSharpFn: vi.fn(),
}));

vi.mock("sharp", () => ({
  default: mockSharpFn,
}));

// --- Imports (after mocks) ---

import { ProcessVariants } from "@d2/files-infra";

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

function makeSharpResult(
  width: number,
  height: number,
  size: number,
  data = Buffer.from("processed"),
) {
  return {
    data,
    info: { width, height, size, format: "webp", channels: 3 },
  };
}

/**
 * Creates a fresh sharp mock pipeline instance.
 * Each call creates an isolated chain: sharp() -> .resize() -> .webp() -> .toBuffer()
 * Returns the individual mock functions for assertions.
 */
function createSharpPipeline(toBufferResults: unknown[]) {
  const resize = vi.fn();
  const webp = vi.fn();
  const toBuffer = vi.fn();

  // Set up chaining
  const webpObj = { toBuffer };
  const resizeObj = { webp: webp.mockReturnValue(webpObj) };
  const sharpObj = { resize: resize.mockReturnValue(resizeObj), webp, toBuffer };

  // Set toBuffer responses
  for (const r of toBufferResults) {
    toBuffer.mockResolvedValueOnce(r);
  }

  mockSharpFn.mockReturnValue(sharpObj);

  return { resize, webp, toBuffer };
}

// --- Tests ---

describe("ProcessVariants", () => {
  let handler: ProcessVariants;

  beforeEach(() => {
    handler = new ProcessVariants(createTestContext());
  });

  it("should process a single variant with resize + webp conversion", async () => {
    const resultBuf = Buffer.from("thumb-data");
    createSharpPipeline([makeSharpResult(64, 64, resultBuf.length, resultBuf)]);

    const variants: VariantConfig[] = [{ name: "thumb", maxDimension: 64 }];

    const result = await handler.handleAsync({
      buffer: Buffer.from("original-image"),
      contentType: "image/jpeg",
      variants,
    });

    expect(result).toBeSuccess();
    expect(result.data?.variants).toHaveLength(1);

    const variant = result.data!.variants[0]!;
    expect(variant.size).toBe("thumb");
    expect(variant.buffer).toEqual(resultBuf);
    expect(variant.width).toBe(64);
    expect(variant.height).toBe(64);
    expect(variant.sizeBytes).toBe(resultBuf.length);
    expect(variant.contentType).toBe("image/webp");
  });

  it("should pass correct resize params to sharp", async () => {
    const { resize } = createSharpPipeline([makeSharpResult(128, 96, 500)]);

    const variants: VariantConfig[] = [{ name: "small", maxDimension: 128 }];

    await handler.handleAsync({
      buffer: Buffer.from("image"),
      contentType: "image/png",
      variants,
    });

    expect(resize).toHaveBeenCalledWith({
      width: 128,
      height: 128,
      fit: "inside",
      withoutEnlargement: true,
    });
  });

  it("should call toBuffer with resolveWithObject: true", async () => {
    const { toBuffer } = createSharpPipeline([makeSharpResult(64, 64, 100)]);

    await handler.handleAsync({
      buffer: Buffer.from("image"),
      contentType: "image/jpeg",
      variants: [{ name: "thumb", maxDimension: 64 }],
    });

    expect(toBuffer).toHaveBeenCalledWith({ resolveWithObject: true });
  });

  it("should process multiple variants", async () => {
    createSharpPipeline([
      makeSharpResult(64, 64, 100),
      makeSharpResult(128, 128, 200),
      makeSharpResult(256, 192, 500),
    ]);

    const variants: VariantConfig[] = [
      { name: "thumb", maxDimension: 64 },
      { name: "small", maxDimension: 128 },
      { name: "medium", maxDimension: 256 },
    ];

    const result = await handler.handleAsync({
      buffer: Buffer.from("image"),
      contentType: "image/jpeg",
      variants,
    });

    expect(result).toBeSuccess();
    expect(result.data?.variants).toHaveLength(3);
    expect(result.data!.variants[0]!.size).toBe("thumb");
    expect(result.data!.variants[1]!.size).toBe("small");
    expect(result.data!.variants[2]!.size).toBe("medium");
  });

  it("should pass through SVG without processing", async () => {
    const svgBuffer = Buffer.from("<svg>...</svg>");

    const variants: VariantConfig[] = [{ name: "thumb", maxDimension: 64 }, { name: "original" }];

    const result = await handler.handleAsync({
      buffer: svgBuffer,
      contentType: "image/svg+xml",
      variants,
    });

    expect(result).toBeSuccess();
    expect(result.data?.variants).toHaveLength(2);

    // SVG variants should return original buffer as-is
    for (const variant of result.data!.variants) {
      expect(variant.buffer).toBe(svgBuffer);
      expect(variant.width).toBe(0);
      expect(variant.height).toBe(0);
      expect(variant.sizeBytes).toBe(svgBuffer.length);
      expect(variant.contentType).toBe("image/svg+xml");
    }

    // sharp should NOT be called for SVG
    expect(mockSharpFn).not.toHaveBeenCalled();
  });

  it("should call webp() for non-SVG images", async () => {
    const { webp } = createSharpPipeline([makeSharpResult(64, 64, 100)]);

    await handler.handleAsync({
      buffer: Buffer.from("jpeg-data"),
      contentType: "image/jpeg",
      variants: [{ name: "thumb", maxDimension: 64 }],
    });

    expect(webp).toHaveBeenCalledTimes(1);
  });

  it("should handle empty variants array", async () => {
    const result = await handler.handleAsync({
      buffer: Buffer.from("image"),
      contentType: "image/jpeg",
      variants: [],
    });

    expect(result).toBeSuccess();
    expect(result.data?.variants).toHaveLength(0);
  });

  it("should preserve variant name from config", async () => {
    createSharpPipeline([makeSharpResult(500, 375, 10_000)]);

    const result = await handler.handleAsync({
      buffer: Buffer.from("image"),
      contentType: "image/png",
      variants: [{ name: "preview", maxDimension: 800 }],
    });

    expect(result).toBeSuccess();
    expect(result.data!.variants[0]!.size).toBe("preview");
  });

  it("should handle SVG with empty variants array", async () => {
    const result = await handler.handleAsync({
      buffer: Buffer.from("<svg/>"),
      contentType: "image/svg+xml",
      variants: [],
    });

    expect(result).toBeSuccess();
    expect(result.data?.variants).toHaveLength(0);
  });

  it("should use maxDimension for both width and height in resize", async () => {
    const { resize } = createSharpPipeline([makeSharpResult(150, 150, 300)]);

    await handler.handleAsync({
      buffer: Buffer.from("image"),
      contentType: "image/webp",
      variants: [{ name: "thumb", maxDimension: 150 }],
    });

    expect(resize).toHaveBeenCalledWith(
      expect.objectContaining({
        width: 150,
        height: 150,
      }),
    );
  });

  it("should set contentType to image/webp for all non-SVG variants", async () => {
    createSharpPipeline([makeSharpResult(64, 64, 50), makeSharpResult(256, 256, 200)]);

    const result = await handler.handleAsync({
      buffer: Buffer.from("png-image"),
      contentType: "image/png",
      variants: [
        { name: "thumb", maxDimension: 64 },
        { name: "medium", maxDimension: 256 },
      ],
    });

    expect(result).toBeSuccess();
    for (const v of result.data!.variants) {
      expect(v.contentType).toBe("image/webp");
    }
  });
});

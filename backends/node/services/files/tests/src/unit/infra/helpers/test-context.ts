import { vi } from "vitest";
import type { IHandlerContext, IRequestContext } from "@d2/handler";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";

/**
 * Creates a minimal mock IHandlerContext for infra handler tests.
 */
export function createTestContext(requestOverrides?: Partial<IRequestContext>): IHandlerContext {
  const request: IRequestContext = {
    isAuthenticated: true,
    isOrgEmulating: null,
    isUserImpersonating: null,
    isTrustedService: null,
    userId: "user-123",
    targetOrgId: "org-456",
    ...requestOverrides,
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

/**
 * Creates a mock Drizzle `db` object with chainable query builders.
 *
 * Defaults to empty results. Use `overrides` to replace specific chains.
 */
export function createMockDb(overrides: Record<string, unknown> = {}) {
  const returning = vi.fn().mockResolvedValue([]);
  const where = vi.fn().mockReturnValue({
    returning,
    limit: vi.fn().mockReturnValue({
      offset: vi.fn().mockReturnValue({
        orderBy: vi.fn().mockResolvedValue([]),
      }),
    }),
  });
  const set = vi.fn().mockReturnValue({ where });
  const values = vi.fn().mockReturnValue({ returning });
  const from = vi.fn().mockReturnValue({
    where,
    limit: vi.fn().mockReturnValue({
      offset: vi.fn().mockReturnValue({
        orderBy: vi.fn().mockResolvedValue([]),
      }),
    }),
  });

  return {
    insert: vi.fn().mockReturnValue({ values }),
    select: vi.fn().mockReturnValue({ from }),
    update: vi.fn().mockReturnValue({ set }),
    delete: vi.fn().mockReturnValue({ where }),
    execute: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  } as unknown as NodePgDatabase;
}

/**
 * Creates a sample FileRow matching the Drizzle schema shape.
 */
export function createSampleFileRow(overrides: Record<string, unknown> = {}) {
  return {
    id: "file-001",
    contextKey: "user_avatar",
    relatedEntityId: "user-123",
    uploaderUserId: "user-123",
    status: "pending",
    contentType: "image/jpeg",
    displayName: "avatar.jpg",
    sizeBytes: 2048,
    variants: undefined,
    rejectionReason: undefined,
    createdAt: new Date("2026-01-15T10:00:00Z"),
    updatedAt: new Date("2026-01-15T10:00:00Z"),
    ...overrides,
  };
}

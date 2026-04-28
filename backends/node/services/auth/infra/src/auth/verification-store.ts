import type { Auth } from "./better-auth/auth-factory.js";

/** Shape returned by BetterAuth's internalAdapter.findVerificationValue. */
interface BetterAuthVerificationValue {
  id: string;
  identifier: string;
  value: string;
  expiresAt: Date;
}

/**
 * BetterAuth-backed verification store.
 *
 * Structurally implements `IVerificationStore` (defined in auth-app) without
 * importing it — avoids circular dependency (infra cannot import from app).
 *
 * Wraps BetterAuth's `auth.$context.internalAdapter` operations on the
 * verification table. We use the table for our own account-change OTP records
 * with structured identifiers like "account-change:email:{userId}".
 *
 * Note: `auth.$context` is a thenable returned by BetterAuth — we await it once
 * per call (cheap; the context is initialized lazily and cached).
 */
export class BetterAuthVerificationStore {
  constructor(private readonly auth: Auth) {}

  async create(input: {
    identifier: string;
    value: string;
    expiresAt: Date;
  }): Promise<BetterAuthVerificationValue> {
    const ctx = await this.auth.$context;
    const created = (await ctx.internalAdapter.createVerificationValue({
      identifier: input.identifier,
      value: input.value,
      expiresAt: input.expiresAt,
    })) as BetterAuthVerificationValue;
    return created;
  }

  async findByIdentifier(identifier: string): Promise<BetterAuthVerificationValue | null> {
    const ctx = await this.auth.$context;
    const found = (await ctx.internalAdapter.findVerificationValue(
      identifier,
    )) as BetterAuthVerificationValue | null;
    return found;
  }

  async updateValue(id: string, newValue: string): Promise<void> {
    const ctx = await this.auth.$context;
    await ctx.internalAdapter.updateVerificationValue(id, {
      value: newValue,
      updatedAt: new Date(),
    });
  }

  async deleteById(id: string): Promise<void> {
    const ctx = await this.auth.$context;
    await ctx.internalAdapter.deleteVerificationValue(id);
  }
}

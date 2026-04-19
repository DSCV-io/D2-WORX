import { verifyPassword as bcryptVerify } from "better-auth/crypto";
import type { Auth } from "./better-auth/auth-factory.js";

interface AccountRow {
  id: string;
  userId: string;
  providerId: string;
  password?: string | null;
}

/**
 * BetterAuth-backed password verifier.
 *
 * Structurally implements `IVerifyUserPassword` (defined in auth-app) without
 * importing it — avoids circular dependency.
 *
 * Looks up the user's credential account via BetterAuth's internalAdapter,
 * then compares the plaintext password against the stored bcrypt hash using
 * BetterAuth's own crypto.
 */
export class BetterAuthPasswordVerifier {
  constructor(private readonly auth: Auth) {}

  async verify(userId: string, plainPassword: string): Promise<boolean> {
    if (!plainPassword) return false;

    const ctx = await this.auth.$context;
    const accounts = (await ctx.internalAdapter.findAccountByUserId(userId)) as AccountRow[];

    // Find the credential account (provider="credential") with a password hash.
    const credential = accounts.find((a) => a.providerId === "credential" && a.password);
    if (!credential?.password) return false;

    return bcryptVerify({ password: plainPassword, hash: credential.password });
  }
}

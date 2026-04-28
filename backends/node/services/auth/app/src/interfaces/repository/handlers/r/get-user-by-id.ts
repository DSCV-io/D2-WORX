import type { IHandler } from "@d2/handler";
import type { UserStatus } from "@d2/auth-domain";

export interface GetUserByIdInput {
  readonly userId: string;
}

export interface GetUserByIdOutput {
  readonly user: {
    readonly id: string;
    readonly email: string;
    readonly emailVerified: boolean;
    /** Display name. `undefined` when the user hasn't set one. */
    readonly name?: string;
    /** Digits-only E.164. `undefined` when the user has no phone. */
    readonly phone?: string;
    readonly phoneVerified: boolean;
    /** BCP-47 locale (e.g. "en-US"). `undefined` when the user hasn't set one. */
    readonly locale?: string;
    /** IANA timezone (e.g. "America/Edmonton"). `undefined` when the user hasn't set one. */
    readonly timezone?: string;
    /** Lifecycle status — used by deletion-flow handlers to decide cancel vs no-op. */
    readonly status: UserStatus;
  };
}

export type IGetUserByIdHandler = IHandler<GetUserByIdInput, GetUserByIdOutput>;

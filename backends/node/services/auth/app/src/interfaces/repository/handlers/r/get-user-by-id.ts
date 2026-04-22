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
    readonly name: string | null;
    readonly phone: string | null;
    readonly phoneVerified: boolean;
    /** BCP-47 locale (e.g. "en-US"). null when user hasn't set one. */
    readonly locale: string | null;
    /** IANA timezone (e.g. "America/Edmonton"). null when user hasn't set one. */
    readonly timezone: string | null;
    /** Lifecycle status — used by deletion-flow handlers to decide cancel vs no-op. */
    readonly status: UserStatus;
  };
}

export type IGetUserByIdHandler = IHandler<GetUserByIdInput, GetUserByIdOutput>;

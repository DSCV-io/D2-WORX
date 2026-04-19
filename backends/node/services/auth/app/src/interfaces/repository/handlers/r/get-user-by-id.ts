import type { IHandler } from "@d2/handler";

export interface GetUserByIdInput {
  readonly userId: string;
}

export interface GetUserByIdOutput {
  readonly user: {
    readonly id: string;
    readonly email: string;
    readonly emailVerified: boolean;
    readonly phone: string | null;
    readonly phoneVerified: boolean;
    /** BCP-47 locale (e.g. "en-US"). null when user hasn't set one. */
    readonly locale: string | null;
  };
}

export type IGetUserByIdHandler = IHandler<GetUserByIdInput, GetUserByIdOutput>;

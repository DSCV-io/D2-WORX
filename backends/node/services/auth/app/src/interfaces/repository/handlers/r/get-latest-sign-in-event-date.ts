import type { IHandler } from "@d2/handler";

export interface GetLatestSignInEventDateInput {
  readonly userId: string;
}

export interface GetLatestSignInEventDateOutput {
  readonly date?: Date;
}

export type IGetLatestSignInEventDateHandler = IHandler<
  GetLatestSignInEventDateInput,
  GetLatestSignInEventDateOutput
>;

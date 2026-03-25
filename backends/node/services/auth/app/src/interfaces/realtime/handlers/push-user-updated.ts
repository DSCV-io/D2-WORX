import type { IHandler } from "@d2/handler";

export interface PushUserUpdatedInput {
  readonly userId: string;
}

export interface PushUserUpdatedOutput {
  readonly delivered: boolean;
}

/** Pushes a user:updated event via SignalR to all active sessions for a user. */
export type IPushUserUpdated = IHandler<PushUserUpdatedInput, PushUserUpdatedOutput>;

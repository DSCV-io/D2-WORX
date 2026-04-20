import type { IHandler } from "@d2/handler";

export interface UpdateSessionWhoIsIdInput {
  readonly id: string;
  readonly whoIsId: string;
}

export interface UpdateSessionWhoIsIdOutput {}

export type IUpdateSessionWhoIsIdHandler = IHandler<
  UpdateSessionWhoIsIdInput,
  UpdateSessionWhoIsIdOutput
>;

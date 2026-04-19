import type { IHandler } from "@d2/handler";
import type {
  AlternativeContactInfo,
  NotifyInput,
  NotifyOutput,
} from "../../handlers/pub/notify.js";

export type { AlternativeContactInfo, NotifyInput, NotifyOutput };

/** Handler interface for publishing notification requests to the Comms service. */
export interface INotifyHandler extends IHandler<NotifyInput, NotifyOutput> {}

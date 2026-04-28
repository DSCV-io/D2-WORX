import { createServiceKey } from "@d2/di";
import type { INotifyHandler } from "./interfaces/pub/notify.js";

export const INotifyKey = createServiceKey<INotifyHandler>("CommsClient.Notify");

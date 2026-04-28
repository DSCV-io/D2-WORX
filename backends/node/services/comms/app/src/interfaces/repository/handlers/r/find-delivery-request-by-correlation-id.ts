import type { IHandler } from "@d2/handler";
import type { DeliveryRequest } from "@d2/comms-domain";

export interface FindDeliveryRequestByCorrelationIdInput {
  readonly correlationId: string;
}

export interface FindDeliveryRequestByCorrelationIdOutput {
  readonly request?: DeliveryRequest;
}

export type IFindDeliveryRequestByCorrelationIdHandler = IHandler<
  FindDeliveryRequestByCorrelationIdInput,
  FindDeliveryRequestByCorrelationIdOutput
>;

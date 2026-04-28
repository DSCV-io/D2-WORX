import { CommsDomainError } from "./comms-domain-error.js";

/**
 * Structured validation error for the Comms domain.
 * Mirrors AuthValidationError / GeoValidationException in .NET.
 */
export class CommsValidationError extends CommsDomainError {
  readonly entityName: string;
  readonly propertyName: string;
  readonly invalidValue: unknown;
  readonly reason: string;

  constructor(entityName: string, propertyName: string, invalidValue: unknown, reason: string) {
    super(`Validation failed for ${entityName}.${propertyName}: ${reason}`);
    this.name = "CommsValidationError";
    this.entityName = entityName;
    this.propertyName = propertyName;
    this.invalidValue = invalidValue;
    this.reason = reason;
  }
}

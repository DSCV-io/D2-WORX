import { FilesDomainError } from "./files-domain-error.js";

/**
 * Structured validation error for the Files domain.
 * Mirrors CommsValidationError / AuthValidationError.
 */
export class FilesValidationError extends FilesDomainError {
  readonly entityName: string;
  readonly propertyName: string;
  readonly invalidValue: unknown;
  readonly reason: string;

  constructor(entityName: string, propertyName: string, invalidValue: unknown, reason: string) {
    super(
      `Validation failed for ${entityName}.${propertyName} with value '${invalidValue}': ${reason}`,
    );
    this.name = "FilesValidationError";
    this.entityName = entityName;
    this.propertyName = propertyName;
    this.invalidValue = invalidValue;
    this.reason = reason;
  }
}

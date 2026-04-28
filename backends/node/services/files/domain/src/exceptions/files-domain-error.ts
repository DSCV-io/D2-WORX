/**
 * Base error type for the Files domain.
 */
export class FilesDomainError extends Error {
  constructor(message: string, options?: ErrorOptions) {
    super(message, options);
    this.name = "FilesDomainError";
  }
}

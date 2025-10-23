namespace Geo.Domain.Exceptions;

/// <summary>
/// Base exception type for all Geography "Geo" Domain exceptions.
/// </summary>
public abstract class GeoDomainException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeoDomainException"/> class with a specified
    /// error message.
    /// </summary>
    ///
    /// <param name="message">
    /// The message that describes the error.
    /// </param>
    protected GeoDomainException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoDomainException"/> class with a specified
    /// error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    ///
    /// <param name="message">
    /// The error message that explains the reason for the exception.
    /// </param>
    ///
    /// <param name="innerException">
    /// The exception that is the cause of the current exception.
    /// </param>
    protected GeoDomainException(string message, Exception innerException)
        : base(message, innerException) { }
}

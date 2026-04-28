// -----------------------------------------------------------------------
// <copyright file="CircuitOpenException.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.CircuitBreaker;

/// <summary>
/// Thrown when a circuit breaker is open and no fallback is provided.
/// </summary>
public class CircuitOpenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitOpenException"/> class
    /// with a default message.
    /// </summary>
    public CircuitOpenException()
        : base("Circuit breaker is open")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitOpenException"/> class
    /// with the specified message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public CircuitOpenException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitOpenException"/> class
    /// with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public CircuitOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

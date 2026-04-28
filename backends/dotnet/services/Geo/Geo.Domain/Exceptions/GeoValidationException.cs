// -----------------------------------------------------------------------
// <copyright file="GeoValidationException.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Domain.Exceptions;

/// <summary>
/// Exception type for validation errors in the Geography "Geo" Domain.
/// </summary>
public class GeoValidationException : GeoDomainException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeoValidationException"/> class.
    /// </summary>
    ///
    /// <param name="objectTypeName">
    /// The name of the object type where the validation error occurred.
    /// </param>
    /// <param name="propertyName">
    /// The name of the property that failed validation.
    /// </param>
    /// <param name="invalidValue">
    /// The invalid value that caused the validation failure.
    /// </param>
    ///
    /// <param name="reason">
    /// The reason why the value is considered invalid.
    /// </param>
    public GeoValidationException(
        string objectTypeName,
        string propertyName,
        object? invalidValue,
        string reason)
        : base($"Validation failed for {objectTypeName}.{propertyName} with value '{invalidValue}': {reason}")
    {
        ObjectTypeName = objectTypeName;
        PropertyName = propertyName;
        InvalidValue = invalidValue;
        Reason = reason;
    }

    /// <summary>
    /// Gets the name of the object type where the validation error occurred.
    /// </summary>
    public string ObjectTypeName { get; }

    /// <summary>
    /// Gets the name of the property that failed validation.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the invalid value that caused the validation failure.
    /// </summary>
    public object? InvalidValue { get; }

    /// <summary>
    /// Gets the reason why the value is considered invalid.
    /// </summary>
    public string Reason { get; }
}

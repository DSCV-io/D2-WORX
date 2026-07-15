// -----------------------------------------------------------------------
// <copyright file="InputFailures.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;

/// <summary>
/// Pre-built <see cref="D2Result"/> input-failure responses for cache impls.
/// Keeps the cache surface pure errors-as-values instead of mixing in
/// <see cref="ArgumentException"/> throws for caller mistakes.
/// </summary>
/// <remarks>
/// Constructors still throw — DI / construction failure is a different
/// lifecycle concern from per-call input validation.
/// </remarks>
public static class InputFailures
{
    /// <summary>
    /// Builds a <see cref="D2Result{TData}"/> input failure for a missing
    /// (null or empty) required parameter.
    /// </summary>
    /// <typeparam name="T">Wrapped result type.</typeparam>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result<T> Required<T>(string paramName)
        => D2Result<T>.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.NOT_NULL_VIOLATION])]);

    /// <summary>
    /// Builds a non-generic <see cref="D2Result"/> input failure for a
    /// missing (null or empty) required parameter.
    /// </summary>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result Required(string paramName)
        => D2Result.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.NOT_NULL_VIOLATION])]);

    /// <summary>
    /// Builds a <see cref="D2Result{TData}"/> input failure for a present but
    /// invalid parameter value (range, non-positive, etc.). Does not use
    /// <see cref="TK.Common.Errors.NOT_NULL_VIOLATION"/>.
    /// </summary>
    /// <typeparam name="T">Wrapped result type.</typeparam>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result<T> Invalid<T>(string paramName)
        => D2Result<T>.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.VALIDATION_FAILED])]);

    /// <summary>
    /// Builds a non-generic <see cref="D2Result"/> input failure for a present
    /// but invalid parameter value (range, non-positive, etc.).
    /// </summary>
    /// <param name="paramName">Parameter name (use <c>nameof</c>).</param>
    public static D2Result Invalid(string paramName)
        => D2Result.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.VALIDATION_FAILED])]);
}

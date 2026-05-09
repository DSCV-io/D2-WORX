// -----------------------------------------------------------------------
// <copyright file="MessagingFailures.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging;

using D2.Shared.I18n;
using D2.Shared.Result;

/// <summary>
/// Pre-built <see cref="D2Result"/> failures for messaging-layer input
/// validation + boundary errors. Mirrors <c>InputFailures</c> in the cache
/// abstractions — keeps the bus surface pure errors-as-values.
/// </summary>
public static class MessagingFailures
{
    /// <summary>Required input parameter was null / empty.</summary>
    /// <param name="paramName">Name of the missing parameter (use <c>nameof</c>).</param>
    public static D2Result Required(string paramName)
        => D2Result.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.NOT_NULL_VIOLATION])]);

    /// <summary>Required input parameter was null / empty (generic overload).</summary>
    /// <typeparam name="T">Wrapped result type.</typeparam>
    /// <param name="paramName">Name of the missing parameter (use <c>nameof</c>).</param>
    public static D2Result<T> Required<T>(string paramName)
        => D2Result<T>.ValidationFailed(
            inputErrors: [new InputError(paramName, [TK.Common.Errors.NOT_NULL_VIOLATION])]);
}

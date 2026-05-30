// -----------------------------------------------------------------------
// <copyright file="IEmailValidator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Validation.Abstractions;

using D2.Shared.Result;

/// <summary>
/// Validates an email address and returns a normalized form on success.
/// </summary>
public interface IEmailValidator
{
    /// <summary>
    /// Validates <paramref name="email"/> and returns the normalized address on success.
    /// </summary>
    /// <param name="email">The email address to validate (may be null or whitespace).</param>
    /// <returns>
    /// <c>Ok</c> wrapping the normalized (trimmed and lowercased) email address on success;
    /// <see cref="D2Result{TData}.ValidationFailed"/> with a per-field
    /// <see cref="D2.Shared.Result.InputError"/> on null, empty, whitespace, or
    /// structurally invalid input.
    /// </returns>
    D2Result<string> Validate(string? email);
}

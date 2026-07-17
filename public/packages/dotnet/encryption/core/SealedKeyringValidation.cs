// -----------------------------------------------------------------------
// <copyright file="SealedKeyringValidation.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Text;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Shared constructor-time validation for the sealed recipient keyrings.
/// The recipient service id grammar mirrors the workload-identity service-id
/// grammar (lowercase <c>[a-z0-9-]</c>, at most 64 characters) — the
/// recipient of a sealed frame IS a workload — without taking a dependency
/// on the workload-identity library (this library stays a pure crypto
/// primitive).
/// </summary>
internal static class SealedKeyringValidation
{
    /// <summary>Maximum recipient service id length in characters.</summary>
    internal const int MAX_SERVICE_ID_LENGTH = 64;

    /// <summary>
    /// Validates the recipient service id grammar: non-empty, at most
    /// <see cref="MAX_SERVICE_ID_LENGTH"/> characters, every character in
    /// lowercase <c>[a-z0-9-]</c>. No normalization — a caller holding an
    /// unvalidated id must validate upstream; this library fails loud.
    /// </summary>
    /// <param name="recipientServiceId">The candidate service id.</param>
    /// <param name="paramName">Parameter name for the thrown exception.</param>
    internal static void ValidateServiceId(string recipientServiceId, string paramName)
    {
        recipientServiceId.ThrowIfFalsey(paramName);

        if (recipientServiceId.Length > MAX_SERVICE_ID_LENGTH)
        {
            throw new ArgumentException(
                $"recipientServiceId length must be at most {MAX_SERVICE_ID_LENGTH} " +
                $"(got {recipientServiceId.Length}).",
                paramName);
        }

        foreach (var c in recipientServiceId)
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-')
                continue;

            throw new ArgumentException(
                "recipientServiceId must match the workload service-id grammar " +
                "(lowercase [a-z0-9-]).",
                paramName);
        }
    }

    /// <summary>
    /// Validates a kid against the sealed frame's kid bounds (the same kid
    /// grammar as the symmetric frame).
    /// </summary>
    /// <param name="kid">The candidate kid.</param>
    /// <param name="paramName">Parameter name for the thrown exception.</param>
    internal static void ValidateKid(string kid, string paramName)
    {
        kid.ThrowIfFalsey(paramName);

        var kidUtf8Length = Encoding.UTF8.GetByteCount(kid);

        if (kidUtf8Length is < SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH
            or > SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH)
        {
            throw new ArgumentException(
                $"kid '{kid}' UTF-8 byte length must be in " +
                $"[{SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH}, " +
                $"{SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH}].",
                paramName);
        }
    }
}

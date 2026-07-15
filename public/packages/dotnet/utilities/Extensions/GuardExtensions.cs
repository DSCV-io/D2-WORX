// -----------------------------------------------------------------------
// <copyright file="GuardExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Extensions;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
/// Guard-clause extension methods that enforce required-argument contracts by
/// throwing <see cref="ArgumentNullException"/> when a value is literally
/// <see langword="null"/>, or <see cref="ArgumentException"/> when it is
/// present-but-falsey (empty / whitespace string, empty collection,
/// <see cref="Guid.Empty"/>).
/// </summary>
/// <remarks>
/// These are programming-error fail-fast guards, not user-facing validation.
/// They throw plain BCL exceptions (not <c>D2Result</c>, not TK messages)
/// because a null-or-falsey required argument is a developer contract violation
/// surfaced at the earliest possible point. The
/// <see cref="System.Diagnostics.CodeAnalysis.NotNullAttribute"/> post-condition
/// means callers can use the value directly after the call without a null-forgiving
/// <c>!</c> operator.
/// <para>
/// The null branch produces <see cref="ArgumentNullException"/> with the standard
/// BCL message <c>"Value cannot be null. (Parameter 'x')"</c>. The
/// present-but-falsey branch produces <see cref="ArgumentException"/> with a
/// developer-readable message naming the violation.
/// </para>
/// <para>
/// Parameter names are auto-captured via
/// <see cref="CallerArgumentExpressionAttribute"/> — never pass the
/// <c>paramName</c> argument manually.
/// </para>
/// <para>
/// Plain reference types (DI services, loggers, options, contexts) have no
/// present-but-falsey semantics beyond <see langword="null"/>; use
/// <see cref="ArgumentNullException.ThrowIfNull(object, string)"/> for those.
/// </para>
/// </remarks>
public static class GuardExtensions
{
    extension([NotNull] string? value)
    {
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when the receiver
        /// is <see langword="null"/>, or <see cref="ArgumentException"/> when it is
        /// empty or contains only whitespace.
        /// </summary>
        /// <param name="paramName">
        /// Auto-captured caller expression — do not supply manually.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the receiver is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the receiver is empty or whitespace-only.
        /// </exception>
        public void ThrowIfFalsey(
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value is null)
                throw new ArgumentNullException(paramName);

            if (value.Falsey())
                throw new ArgumentException("Value must be a non-empty, non-whitespace string.", paramName);
        }
    }

    extension<T>([NotNull] IEnumerable<T>? value)
    {
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when the receiver
        /// is <see langword="null"/>, or <see cref="ArgumentException"/> when the
        /// collection contains zero elements.
        /// </summary>
        /// <param name="paramName">
        /// Auto-captured caller expression — do not supply manually.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the receiver is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the receiver is an empty collection.
        /// </exception>
        public void ThrowIfFalsey(
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value is null)
                throw new ArgumentNullException(paramName);

            if (value.Falsey())
                throw new ArgumentException("Collection must contain at least one element.", paramName);
        }
    }

    extension([NotNull] Guid? value)
    {
        /// <summary>
        /// Throws <see cref="ArgumentNullException"/> when the receiver
        /// is <see langword="null"/>, or <see cref="ArgumentException"/> when it
        /// equals <see cref="Guid.Empty"/>.
        /// </summary>
        /// <param name="paramName">
        /// Auto-captured caller expression — do not supply manually.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the receiver is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the receiver equals <see cref="Guid.Empty"/>.
        /// </exception>
        public void ThrowIfFalsey(
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value is null)
                throw new ArgumentNullException(paramName);

            if (value.Falsey())
                throw new ArgumentException("Value must not be Guid.Empty.", paramName);
        }
    }

    extension(Guid value)
    {
        /// <summary>
        /// Throws <see cref="ArgumentException"/> when the receiver
        /// equals <see cref="Guid.Empty"/>.
        /// </summary>
        /// <param name="paramName">
        /// Auto-captured caller expression — do not supply manually.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when the receiver equals <see cref="Guid.Empty"/>.
        /// </exception>
        public void ThrowIfFalsey(
            [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value.Falsey())
                throw new ArgumentException("Value must not be Guid.Empty.", paramName);
        }
    }
}

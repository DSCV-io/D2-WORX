// -----------------------------------------------------------------------
// <copyright file="Validators.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

using System.Net;
using D2.Shared.I18n;
using D2.Shared.Utilities.Extensions;
using FluentValidation;

/// <summary>
/// Common FluentValidation extensions for reuse across service validators.
/// </summary>
public static class Validators
{
    extension<T>(IRuleBuilder<T, string> rule)
    {
        /// <summary>
        /// Validates that the string is a valid IPv4 or IPv6 address.
        /// </summary>
        public IRuleBuilderOptions<T, string> IsValidIpAddress()
            => rule.Must(ip => ip.Truthy() && IPAddress.TryParse(ip, out _))
                .WithMessage(TK.Common.Validation.IP_INVALID);

        /// <summary>
        /// Validates that the string is a 64-character hex SHA-256 hash ID.
        /// </summary>
        public IRuleBuilderOptions<T, string> IsValidHashId()
            => rule.Length(64)
                .Matches("^[a-fA-F0-9]+$")
                .WithMessage(TK.Common.Validation.HASH_ID_INVALID);

        /// <summary>
        /// Validates that the string is a valid GUID/UUID.
        /// </summary>
        public IRuleBuilderOptions<T, string> IsValidGuid()
            => rule.Must(id => Guid.TryParse(id, out var g) && g != Guid.Empty)
                .WithMessage(TK.Common.Validation.ID_INVALID);

        /// <summary>
        /// Validates that the string is a valid email address format.
        /// </summary>
        public IRuleBuilderOptions<T, string> IsValidEmail()
            => rule.EmailAddress()
                .WithMessage(TK.Common.Validation.EMAIL_INVALID);

        /// <summary>
        /// Validates that the string contains 7-15 digits (E.164 compatible).
        /// </summary>
        public IRuleBuilderOptions<T, string> IsValidPhoneE164()
            => rule.Matches(@"^\d{7,15}$")
                .WithMessage(TK.Common.Validation.PHONE_INVALID);

        /// <summary>
        /// Validates that the string is in the allowed context key list.
        /// Empty list disables validation (always passes).
        /// </summary>
        public IRuleBuilderOptions<T, string> IsAllowedContextKey(List<string> allowed)
            => rule.Must(key => allowed.Count == 0 || allowed.Contains(key))
                .WithMessage(TK.Common.Validation.CONTEXT_KEY_NOT_ALLOWED);
    }

    extension<T, TItem>(IRuleBuilder<T, IList<TItem>> rule)
    {
        /// <summary>
        /// Validates that the list is not null or empty.
        /// </summary>
        public IRuleBuilderOptions<T, IList<TItem>> IsNonEmpty()
            => rule.NotEmpty()
                .WithMessage(TK.Common.Validation.NON_EMPTY_LIST);
    }

    extension<T, TItem>(IRuleBuilder<T, IReadOnlyList<TItem>> rule)
    {
        /// <summary>
        /// Validates that the read-only list is not null or empty.
        /// </summary>
        public IRuleBuilderOptions<T, IReadOnlyList<TItem>> IsNonEmpty()
            => rule.NotEmpty()
                .WithMessage(TK.Common.Validation.NON_EMPTY_LIST);
    }

    extension<T, TItem>(IRuleBuilder<T, List<TItem>> rule)
    {
        /// <summary>
        /// Validates that the list is not null or empty.
        /// </summary>
        public IRuleBuilderOptions<T, List<TItem>> IsNonEmpty()
            => rule.NotEmpty()
                .WithMessage(TK.Common.Validation.NON_EMPTY_LIST);
    }
}

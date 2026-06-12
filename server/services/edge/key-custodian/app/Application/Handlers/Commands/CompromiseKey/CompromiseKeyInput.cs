// -----------------------------------------------------------------------
// <copyright file="CompromiseKeyInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;

/// <summary>
/// Input to <c>CompromiseKey</c>: the kid of the live key to mark compromised,
/// the operator reason, and whether to auto-generate a replacement pending key.
/// </summary>
/// <remarks>
/// <b>PII / <see cref="Reason"/>.</b> The operator reason can carry sensitive
/// context (a person's name, an internal system). It is marked
/// <c>[RedactData(PersonalInformation)]</c> for the Serilog destructuring layer,
/// and <see cref="ToString"/> is overridden so the reason never appears in any
/// log line or handler I/O trace.
/// </remarks>
public sealed record CompromiseKeyInput
{
    /// <summary>Gets the live key's identifier.</summary>
    public string? Kid { get; init; }

    /// <summary>Gets the operator-supplied reason for compromising the key. Redacted from logs.
    /// </summary>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public string? Reason { get; init; }

    /// <summary>
    /// Gets a value indicating whether a replacement pending key is generated for
    /// the same domain. Defaults to <see langword="true"/>.
    /// </summary>
    public bool GenerateReplacement { get; init; } = true;

    /// <inheritdoc/>
    public override string ToString()
    {
        // long format string — cannot wrap
        return string.Create(
            CultureInfo.InvariantCulture,
            $"CompromiseKeyInput {{ Kid = {Kid}, Reason = [REDACTED:PersonalInformation], GenerateReplacement = {GenerateReplacement} }}");
    }

    /// <summary>
    /// Overrides auto-generated <c>PrintMembers</c> so the reason never appears in
    /// record equality / debug output.
    /// </summary>
    /// <param name="builder">The string builder used by the record's printer.</param>
    /// <returns><see langword="true"/> per the record-printer contract.</returns>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        // long format string — cannot wrap
        builder.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"Kid = {Kid}, Reason = [REDACTED:PersonalInformation], GenerateReplacement = {GenerateReplacement}"));
        return true;
    }
}

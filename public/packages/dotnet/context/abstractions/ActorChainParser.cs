// -----------------------------------------------------------------------
// <copyright file="ActorChainParser.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using System.Collections.Generic;
using System.Text.Json;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Parses the RFC 8693 §2.1 nested <c>act</c> claim into a flat
/// <see cref="IReadOnlyList{ActorEntry}"/> ordered outermost-first per RFC
/// 8693 §4.1 ("the outermost act claim represents the current actor; the
/// least recent actor is the most deeply nested").
/// </summary>
/// <remarks>
/// <para>
/// <b>Strict-mode parsing.</b> Any structural malformation throws
/// <see cref="MalformedActorChainException"/> rather than silently returning a
/// degraded chain — silent fallbacks for signed-token bugs would mask broken
/// upstream mint logic. Required-claim checks (see below) fire on every entry.
/// Auth middleware MUST catch and convert to 401 Unauthorized — a malformed
/// actor chain is a signed-token-with-bad-payload condition, not a handler bug.
/// </para>
/// <para>
/// <b>Required claims per entry:</b>
/// <list type="bullet">
///   <item>Every entry: <c>sub</c> (per RFC 8693 §2.1 "MUST contain a sub claim").</item>
///   <item>Service entries (no <c>d2_kind</c>): just <c>sub</c>.</item>
///   <item>Impersonation entries (<c>d2_kind</c> present): <c>sub</c> + <c>d2_kind</c> +
///   <c>d2_session_id</c> + <c>d2_org_id</c> + <c>d2_org_type</c> + <c>d2_org_role</c>.</item>
/// </list>
/// </para>
/// <para>
/// <b>Depth limit:</b> <see cref="MaxActDepth"/> (20). Beyond that we throw —
/// real-world delegation chains are 1–4 hops; anything past 10 is suspicious;
/// 20 is a hard wall against DoS via deeply-nested act claims.
/// </para>
/// </remarks>
public static class ActorChainParser
{
    /// <summary>Maximum nesting depth for the act chain. Exceeding throws.</summary>
    public const int MaxActDepth = 20;

    private const string _SUB = "sub";
    private const string _CLIENT_ID = "client_id";
    private const string _D2_KIND = "d2_kind";
    private const string _D2_SESSION_ID = "d2_session_id";
    private const string _D2_ORG_ID = "d2_org_id";
    private const string _D2_ORG_NAME = "d2_org_name";
    private const string _D2_ORG_TYPE = "d2_org_type";
    private const string _D2_ORG_ROLE = "d2_org_role";
    private const string _ACT = "act";

    private static readonly IReadOnlyList<ActorEntry> sr_empty = [];

    /// <summary>
    /// Parses a JSON-encoded <c>act</c> claim string into a flat actor chain.
    /// Returns empty when <paramref name="json"/> is null/empty/whitespace.
    /// </summary>
    /// <param name="json">
    /// The JSON-encoded actor object (e.g. the value of the <c>act</c> claim
    /// from <c>ClaimsPrincipal</c>).
    /// </param>
    /// <returns>The flat actor chain (outermost first).</returns>
    /// <exception cref="MalformedActorChainException">
    /// Thrown when the JSON is malformed, the chain exceeds
    /// <see cref="MaxActDepth"/>, or any entry is missing a required claim.
    /// </exception>
    public static IReadOnlyList<ActorEntry> ParseFromJsonString(string? json)
    {
        if (json.Falsey())
            return sr_empty;

        try
        {
            using var doc = JsonDocument.Parse(json!);
            return ParseFromJson(doc.RootElement);
        }
        catch (JsonException ex)
        {
            throw new MalformedActorChainException(
                "Actor chain JSON is malformed and cannot be parsed.",
                ex);
        }
    }

    /// <summary>
    /// Parses an <see cref="JsonElement"/> representing the <c>act</c> claim
    /// into a flat actor chain. Returns empty when
    /// <paramref name="actElement"/>'s ValueKind is Undefined (i.e. the caller
    /// passed a non-existent property).
    /// </summary>
    /// <param name="actElement">The <c>act</c> claim element from a JWT payload.</param>
    /// <returns>The flat actor chain (outermost first).</returns>
    /// <exception cref="MalformedActorChainException">
    /// Thrown when the element is present but not an object, the chain exceeds
    /// <see cref="MaxActDepth"/>, or any entry is missing a required claim.
    /// </exception>
    public static IReadOnlyList<ActorEntry> ParseFromJson(JsonElement actElement)
    {
        // Undefined means TryGetProperty returned false — caller didn't have
        // an act claim. Legitimate; return empty.
        if (actElement.ValueKind == JsonValueKind.Undefined)
            return sr_empty;

        // Anything else non-Object is malformed per RFC 8693 §2.1 (act is an object).
        if (actElement.ValueKind != JsonValueKind.Object)
        {
            throw new MalformedActorChainException(
                $"Actor chain root must be a JSON object per RFC 8693 §2.1, "
                + $"got {actElement.ValueKind}.");
        }

        var chain = new List<ActorEntry>();
        var current = actElement;
        var depth = 0;

        while (current.ValueKind == JsonValueKind.Object)
        {
            depth++;
            if (depth > MaxActDepth)
            {
                throw new MalformedActorChainException(
                    $"Actor chain depth exceeds maximum {MaxActDepth} (DoS protection).");
            }

            chain.Add(BuildEntry(current, depth));

            if (!current.TryGetProperty(_ACT, out var nextAct))
                break;

            // Nested act must also be an object (or absent) — same rule.
            if (nextAct.ValueKind != JsonValueKind.Object
                && nextAct.ValueKind != JsonValueKind.Undefined)
            {
                throw new MalformedActorChainException(
                    $"Nested act at depth {depth + 1} must be a JSON object, "
                    + $"got {nextAct.ValueKind}.");
            }

            current = nextAct;
        }

        return chain;
    }

    private static ActorEntry BuildEntry(JsonElement element, int depth)
    {
        // Per RFC 8693 §2.1: every act entry MUST contain a sub claim.
        var subject = TryGetString(element, _SUB);
        if (subject.Falsey())
        {
            throw new MalformedActorChainException(
                $"Actor entry at depth {depth} missing required 'sub' claim "
                + "per RFC 8693 §2.1.");
        }

        var clientId = TryGetString(element, _CLIENT_ID);
        var orgName = TryGetString(element, _D2_ORG_NAME);
        var kindStr = TryGetString(element, _D2_KIND);

        ActorKind kind;
        ImpersonationKind? impKind = null;
        Guid? sessionId = null;
        Guid? orgId = null;
        OrgType? orgType = null;
        Role? orgRole = null;

        if (kindStr.Falsey())
        {
            // No d2_kind → Service entry (RFC 6749 §4.4 client_credentials
            // propagated through delegation).
            kind = ActorKind.Service;
        }
        else
        {
            // d2_kind present → Impersonation entry. Must parse to a valid
            // ImpersonationKind value.
            if (!kindStr.TryParseTruthyNull(out impKind))
            {
                throw new MalformedActorChainException(
                    $"Actor entry at depth {depth} has invalid 'd2_kind' value "
                    + $"'{kindStr}' (expected Consent or Force).");
            }

            kind = ActorKind.Impersonation;

            // Impersonation entries MUST carry session + agent's org context.
            var sessionIdStr = TryGetString(element, _D2_SESSION_ID);
            if (!sessionIdStr.TryParseTruthyNull(out sessionId))
            {
                throw new MalformedActorChainException(
                    $"Impersonation entry at depth {depth} missing or invalid "
                    + "'d2_session_id' (required to track impersonation session).");
            }

            var orgIdStr = TryGetString(element, _D2_ORG_ID);
            if (!orgIdStr.TryParseTruthyNull(out orgId))
            {
                throw new MalformedActorChainException(
                    $"Impersonation entry at depth {depth} missing or invalid "
                    + "'d2_org_id' (required for impersonator audit).");
            }

            var orgTypeStr = TryGetString(element, _D2_ORG_TYPE);
            if (!orgTypeStr.TryParseTruthyNull(out orgType))
            {
                throw new MalformedActorChainException(
                    $"Impersonation entry at depth {depth} missing or invalid "
                    + "'d2_org_type' (required for impersonator audit).");
            }

            var orgRoleStr = TryGetString(element, _D2_ORG_ROLE);
            if (!orgRoleStr.TryParseTruthyNull(out orgRole))
            {
                throw new MalformedActorChainException(
                    $"Impersonation entry at depth {depth} missing or invalid "
                    + "'d2_org_role' (required for impersonator audit).");
            }

            // OrgName is a display string — preserved if present, but not
            // strictly required for audit (org_id + org_type carry the load).
        }

        return new ActorEntry(
            Kind: kind,
            Subject: subject!,
            ClientId: clientId,
            ImpersonationKind: impKind,
            SessionId: sessionId,
            OrgId: orgId,
            OrgName: orgName,
            OrgType: orgType,
            OrgRole: orgRole,
            Act: null);
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}

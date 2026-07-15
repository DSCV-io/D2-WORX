// -----------------------------------------------------------------------
// <copyright file="ValidationFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Loads a hand-authored cross-language validation parity corpus from
/// <c>contracts/validation/fixtures/&lt;name&gt;.json</c> and exposes its rows
/// as a strongly-typed model. The corpus is the SOURCE OF TRUTH shared with
/// the TypeScript <c>@dcsv-io/d2-validation</c> parity tests — both runtimes read the
/// SAME file and assert the SAME expected behavior per row.
/// </summary>
/// <remarks>
/// Mirrors the <c>LocationHashDeterminismTests</c> fixture-loading pattern:
/// the path is derived from <see cref="CallerFilePathAttribute"/> at compile
/// time (robust under hermetic CI sandboxes), the JSON is parsed manually via
/// <see cref="JsonDocument"/> (explicit, no reflection surprises), and a guard
/// throws if zero rows load.
/// </remarks>
internal static class ValidationFixture
{
    /// <summary>
    /// Loads and parses the named corpus. Throws if the file is missing or
    /// contains zero rows.
    /// </summary>
    /// <param name="name">The corpus name (<c>email</c>, <c>phone</c>, or
    /// <c>postcode</c>) — the file stem under
    /// <c>contracts/validation/fixtures/</c>.</param>
    /// <returns>The parsed corpus.</returns>
    public static ValidationCorpus Load(string name)
    {
        var path = ResolveFixturePath(name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Validation corpus '{name}' not found at expected path '{path}' " +
                "(derived from [CallerFilePath]; should resolve to " +
                $"contracts/validation/fixtures/{name}.json relative to this " +
                "test source file).");
        }

        var json = File.ReadAllText(path);
        return Parse(name, json);
    }

    /// <summary>
    /// Synthesizes the validator input for a row. Mirrors the TypeScript
    /// <c>synthInput</c> exactly: <c>null</c> kind => empty string,
    /// <c>whitespace</c> => spaces, <c>oversized</c> =>
    /// <c>char</c> repeated <c>inputRepeat</c> times plus optional
    /// <c>suffix</c>. A literal <c>input</c> is returned verbatim.
    /// </summary>
    /// <param name="row">The corpus row.</param>
    /// <returns>The synthesized input (may be empty; never <see langword="null"/>
    /// for a synthesized kind — an empty string drives the falsey path the
    /// same way <c>undefined</c> does on the TS side).</returns>
    public static string? SynthInput(ValidationRow row)
    {
        if (row.InputKind is null)
            return row.Input;

        return row.InputKind switch
        {
            "null" => string.Empty,
            "whitespace" => "   ",
            "oversized" => new string((row.Char ?? "a")[0], row.InputRepeat ?? 1)
                + (row.Suffix ?? string.Empty),
            _ => throw new InvalidOperationException(
                $"Unknown inputKind '{row.InputKind}' on row '{row.Name}'."),
        };
    }

    private static ValidationCorpus Parse(string name, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var corpus = new ValidationCorpus
        {
            Version = root.TryGetProperty("version", out var verEl)
                ? verEl.GetString() ?? string.Empty
                : string.Empty,
            Validator = root.TryGetProperty("validator", out var valEl)
                ? valEl.GetString() ?? string.Empty
                : string.Empty,
        };

        if (!root.TryGetProperty("rows", out var rowsEl)
            || rowsEl.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Corpus '{name}' has no 'rows' array.");
        }

        foreach (var rowEl in rowsEl.EnumerateArray())
        {
            corpus.Rows.Add(new ValidationRow
            {
                Name = rowEl.GetProperty("name").GetString() ?? string.Empty,
                Input = rowEl.TryGetProperty("input", out var inEl)
                    ? inEl.GetString() : null,
                InputKind = rowEl.TryGetProperty("inputKind", out var ikEl)
                    ? ikEl.GetString() : null,
                Char = rowEl.TryGetProperty("char", out var chEl)
                    ? chEl.GetString() : null,
                InputRepeat = rowEl.TryGetProperty("inputRepeat", out var irEl)
                    && irEl.ValueKind == JsonValueKind.Number
                    ? irEl.GetInt32() : null,
                Suffix = rowEl.TryGetProperty("suffix", out var sfEl)
                    ? sfEl.GetString() : null,
                Country = rowEl.TryGetProperty("country", out var coEl)
                    ? coEl.GetString() : null,
                Valid = rowEl.GetProperty("valid").GetBoolean(),
                Normalized = rowEl.TryGetProperty("normalized", out var noEl)
                    ? noEl.GetString() : null,
                ErrorKey = rowEl.TryGetProperty("errorKey", out var ekEl)
                    ? ekEl.GetString() : null,
            });
        }

        if (corpus.Rows.Falsey())
        {
            var preview = json.Length > 200 ? json[..200] + "..." : json;
            throw new InvalidOperationException(
                $"Corpus '{name}' deserialized with zero rows. " +
                $"version='{corpus.Version}'. JSON preview: {preview}");
        }

        return corpus;
    }

    private static string ResolveFixturePath(
        string name,
        [CallerFilePath] string thisSourcePath = "")
    {
        // Walk up from this source file's directory until the repo root is
        // found — identified by a '.git' entry (only present at the true
        // root, unlike 'D2.slnx' which sits in server/). In a primary clone
        // '.git' is a DIRECTORY; in a linked git worktree it is a FILE
        // pointing at the shared gitdir — accept both so the suite runs in
        // worktrees. This is robust under any depth rearrangement of the
        // tests folder — no fixed iteration count, no silent wrong-root on
        // partial matches.
        var dir = Path.GetDirectoryName(thisSourcePath) ?? string.Empty;
        while (dir.Truthy())
        {
            var gitEntry = Path.Combine(dir, ".git");

            if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
            {
                return Path.Combine(
                    dir,
                    "public",
                    "contracts",
                    "validation",
                    "fixtures",
                    $"{name}.json");
            }

            var parent = Path.GetDirectoryName(dir);

            if (parent == dir)
            {
                break; // filesystem root reached
            }

            dir = parent ?? string.Empty;
        }

        throw new InvalidOperationException(
            $"Could not locate the repo root (no '.git' entry found) " +
            $"walking up from '{Path.GetDirectoryName(thisSourcePath)}'. " +
            $"The fixture path for corpus '{name}' cannot be resolved.");
    }
}

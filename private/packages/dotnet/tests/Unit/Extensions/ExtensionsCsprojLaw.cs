// -----------------------------------------------------------------------
// <copyright file="ExtensionsCsprojLaw.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// Pure helpers that assert Extensions host / consumer / isolation package law
/// against csproj XML text (real files or deliberate-drift fixtures).
/// </summary>
internal static class ExtensionsCsprojLaw
{
    public static IReadOnlyList<string> ProjectReferenceIncludes(string csprojXml)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();
    }

    public static IReadOnlyList<string> AdditionalFilesIncludes(string csprojXml)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants("AdditionalFiles")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();
    }

    public static bool HasProperty(string csprojXml, string name, string expectedValue)
    {
        var doc = XDocument.Parse(csprojXml);
        var value = doc.Descendants(name).Select(e => e.Value).FirstOrDefault();

        return string.Equals(value, expectedValue, StringComparison.Ordinal);
    }

    public static bool HasCompileRemoveGenerated(string csprojXml)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants("Compile")
            .Any(e =>
            {
                var remove = (string?)e.Attribute("Remove") ?? string.Empty;

                return remove.Contains(
                    "CompilerGeneratedFilesOutputPath",
                    StringComparison.Ordinal);
            });
    }

    public static bool IsAnalyzerProjectReference(string csprojXml, string pathFragment)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants("ProjectReference")
            .Any(e =>
            {
                var include = (string?)e.Attribute("Include") ?? string.Empty;
                var output = (string?)e.Attribute("OutputItemType") ?? string.Empty;

                return include.Contains(pathFragment, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(output, "Analyzer", StringComparison.Ordinal);
            });
    }

    public static bool HasTwinProjectReference(string csprojXml, string publicTwinPathFragment)
    {
        return ProjectReferenceIncludes(csprojXml)
            .Any(i =>
                i.Contains(publicTwinPathFragment, StringComparison.OrdinalIgnoreCase)
                && !IsAnalyzerOnlyInclude(csprojXml, i));
    }

    public static bool ReferencesExtensionsPackage(string csprojXml, string packageTail)
    {
        return ProjectReferenceIncludes(csprojXml)
            .Any(i =>
                i.Contains(packageTail, StringComparison.OrdinalIgnoreCase)
                || i.Contains(
                    packageTail.Replace('/', '\\'),
                    StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasBagReferencePath(string csprojXml)
    {
        return ProjectReferenceIncludes(csprojXml)
            .Any(i =>
                i.Contains("product-constants", StringComparison.OrdinalIgnoreCase)
                || i.Contains(
                    "DcsvIo.D2.Private.ProductConstants",
                    StringComparison.OrdinalIgnoreCase)
                || i.Contains(
                    "i18n-keys\\DcsvIo.D2.Private.I18n.Keys",
                    StringComparison.OrdinalIgnoreCase)
                || i.Contains(
                    "i18n-keys/DcsvIo.D2.Private.I18n.Keys",
                    StringComparison.OrdinalIgnoreCase)
                || i.Contains(
                    "DcsvIo.D2.Private.I18n.Keys.csproj",
                    StringComparison.OrdinalIgnoreCase));
    }

    public static bool PublicProjectReferencesPrivateOrExtensions(string csprojXml)
    {
        return ProjectReferenceIncludes(csprojXml)
            .Any(i =>
            {
                if (i.Contains("Microsoft.Extensions", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return i.Contains("private/", StringComparison.OrdinalIgnoreCase)
                    || i.Contains("private\\", StringComparison.OrdinalIgnoreCase)
                    || i.Contains("D2PrivatePackages", StringComparison.OrdinalIgnoreCase)
                    || i.Contains(".Extensions.csproj", StringComparison.OrdinalIgnoreCase)
                    || i.Contains(
                        "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
                        StringComparison.OrdinalIgnoreCase)
                    || i.Contains(
                        "DcsvIo.D2.Private.Encryption.Extensions",
                        StringComparison.OrdinalIgnoreCase)
                    || i.Contains(
                        "DcsvIo.D2.Private.I18n.Keys.Extensions",
                        StringComparison.OrdinalIgnoreCase);
            });
    }

    private static bool IsAnalyzerOnlyInclude(string csprojXml, string include)
    {
        var doc = XDocument.Parse(csprojXml);

        return doc.Descendants("ProjectReference")
            .Any(e =>
            {
                var i = (string?)e.Attribute("Include") ?? string.Empty;
                var output = (string?)e.Attribute("OutputItemType") ?? string.Empty;

                return string.Equals(i, include, StringComparison.Ordinal)
                    && string.Equals(output, "Analyzer", StringComparison.Ordinal);
            });
    }
}

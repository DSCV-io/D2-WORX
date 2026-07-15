// -----------------------------------------------------------------------
// <copyright file="TreeNode.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System;
using System.Collections.Generic;

/// <summary>
/// Mutable tree node used by <see cref="ScopesEmitter"/> to build the
/// nested-class structure during emission. Each segment of every scope name
/// carves a path; the final segment becomes a constant on the leaf node, and
/// intermediate segments become nested classes.
/// </summary>
internal sealed class TreeNode
{
    /// <summary>Initializes a new instance of the <see cref="TreeNode"/> class.</summary>
    /// <param name="segment">The PascalCase segment name (empty for the tree root).</param>
    public TreeNode(string segment)
    {
        Segment = segment;
    }

    /// <summary>Gets the PascalCase segment name (empty for the tree root).</summary>
    public string Segment { get; }

    /// <summary>Gets the child sub-tree nodes by PascalCase segment name.</summary>
    public SortedDictionary<string, TreeNode> Children { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the leaf-level constants on this node, keyed by PascalCase constant name.
    /// </summary>
    public Dictionary<string, ScopeEntry> Constants { get; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a scope to the tree by walking its dot-separated segments, creating
    /// child nodes as needed and recording the leaf-segment as a constant.
    /// </summary>
    /// <param name="scope">The scope entry to add.</param>
    public void AddScope(ScopeEntry scope)
    {
        var segments = scope.Name.Split('.');
        var node = this;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var pascal = ToPascalCase(segments[i]);
            if (!node.Children.TryGetValue(pascal, out var child))
            {
                child = new TreeNode(pascal);
                node.Children[pascal] = child;
            }

            node = child;
        }

        var leafName = ToPascalCase(segments[segments.Length - 1]);
        node.Constants[leafName] = scope;
    }

    private static string ToPascalCase(string segment) =>
        segment.Length == 0
            ? segment
            : char.ToUpperInvariant(segment[0]) + segment.Substring(1);
}

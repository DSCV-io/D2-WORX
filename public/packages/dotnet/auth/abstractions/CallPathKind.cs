// -----------------------------------------------------------------------
// <copyright file="CallPathKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>The kind of hop a <see cref="CallPathEntry"/> records.</summary>
public enum CallPathKind
{
    /// <summary>The Edge HTTP inbound boundary (the originating hop).</summary>
    Edge = 0,

    /// <summary>A cross-process workload hop reached over authenticated mutual TLS.</summary>
    WorkloadHop = 1,

    /// <summary>An in-process module hop (the in-host leaf).</summary>
    ModuleHop = 2,

    /// <summary>An in-host system worker (a background service).</summary>
    System = 3,
}

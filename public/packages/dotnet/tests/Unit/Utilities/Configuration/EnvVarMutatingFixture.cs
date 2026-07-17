// -----------------------------------------------------------------------
// <copyright file="EnvVarMutatingFixture.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Configuration;

using Xunit;

/// <summary>
/// Serializes any test class decorated with <c>[Collection("EnvVarMutating")]</c>.
/// xUnit runs collections single-threaded; tests within remain isolated from
/// each other's environment-variable mutations.
/// </summary>
[CollectionDefinition("EnvVarMutating", DisableParallelization = true)]
public sealed class EnvVarMutatingFixture
{
}

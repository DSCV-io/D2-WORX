// -----------------------------------------------------------------------
// <copyright file="ICacheSerializer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Caching;

using DcsvIo.D2.Result;

/// <summary>
/// Pluggable serialization seam for distributed caches. Local caches store
/// objects directly and don't need this. The default implementation in
/// the distributed lib is JSON; provider-specific impls may swap in
/// MessagePack, Protobuf, etc., for size or perf wins.
/// </summary>
public interface ICacheSerializer
{
    /// <summary>
    /// Gets a stable identifier for the serialization format (e.g.
    /// <c>"application/json"</c>). Allows mixed-serializer DLQs / archive
    /// blobs to round-trip safely.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Serializes <paramref name="value"/> to bytes.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Value to serialize.</param>
    /// <returns>
    /// <c>Ok(bytes)</c>; failure with <c>COULD_NOT_BE_SERIALIZED</c> on
    /// serializer error.
    /// </returns>
    D2Result<byte[]> Serialize<T>(T value);

    /// <summary>
    /// Deserializes <paramref name="bytes"/> back to a value.
    /// </summary>
    /// <typeparam name="T">Expected value type.</typeparam>
    /// <param name="bytes">Stored bytes.</param>
    /// <returns>
    /// <c>Ok(value)</c>; failure with <c>COULD_NOT_BE_DESERIALIZED</c> on
    /// deserializer error.
    /// </returns>
    D2Result<T> Deserialize<T>(byte[] bytes);
}

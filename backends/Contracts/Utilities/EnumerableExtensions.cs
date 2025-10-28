namespace D2.Contracts.Utilities;

/// <summary>
/// Extension methods for enumerables.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Determines if an enumerable is "truthy" (i.e., not null and contains at least one element).
    /// </summary>
    ///
    /// <param name="enumerable">
    /// The enumerable to check.
    /// </param>
    ///
    /// <typeparam name="T">
    /// The type of elements in the enumerable.
    /// </typeparam>
    ///
    /// <returns>
    /// True if the enumerable is not null and contains at least one element; otherwise, false.
    /// </returns>
    public static bool Truthy<T>(this IEnumerable<T>? enumerable) => enumerable?.Any() ?? false;

    /// <summary>
    /// Determines if an enumerable is "falsey" (i.e., null or contains no elements).
    /// </summary>
    ///
    /// <param name="enumerable">
    /// The enumerable to check.
    /// </param>
    ///
    /// <typeparam name="T">
    /// The type of elements in the enumerable.
    /// </typeparam>
    ///
    /// <returns>True if the enumerable is null or contains no elements; otherwise, false.</returns>
    public static bool Falsey<T>(this IEnumerable<T>? enumerable) => !Truthy(enumerable);

    /// <summary>
    /// Cleans an enumerable by applying a cleaner function to each element and handling nulls.
    /// </summary>
    ///
    /// <param name="enumerable">
    /// The enumerable.
    /// </param>
    /// <param name="cleaner">
    /// The cleaner function.
    /// </param>
    /// <param name="enumEmptyBehavior">
    /// The chosen enumerable behavior if the enumerable is null or empty before or after cleaning.
    /// </param>
    /// <param name="valueNullBehavior">
    /// The chosen behavior if a cleaned value is null.
    /// </param>
    ///
    /// <typeparam name="T">
    /// The type to clean.
    /// </typeparam>
    ///
    /// <returns>
    /// The cleaned enumerable.
    /// </returns>
    ///
    /// <exception cref="NullReferenceException">
    /// Thrown if a cleaned value is null and <paramref name="valueNullBehavior"/> is
    /// <see cref="CleanValueNullBehavior.ThrowOnNull"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the enumerable is null or empty and <paramref name="enumEmptyBehavior"/> is
    /// <see cref="CleanEnumEmptyBehavior.Throw"/>.
    /// </exception>
    public static IEnumerable<T>? Clean<T>(
        this IEnumerable<T>? enumerable,
        Func<T, T?> cleaner,
        CleanEnumEmptyBehavior enumEmptyBehavior = CleanEnumEmptyBehavior.ReturnNull,
        CleanValueNullBehavior valueNullBehavior = CleanValueNullBehavior.RemoveNulls)
    {
        // Convert to list to avoid multiple enumerations.
        var dirtyValues = enumerable?.ToList();

        // If the input enumerable is null or empty, handle according to the specified behavior.
        if (dirtyValues.Falsey())
            return HandleCleanEmptyBehavior<T>(nameof(enumerable), enumEmptyBehavior);

        // Initialize a list to hold cleaned values.
        List<T> cleanList = [];

        // Iterate through each value, clean it, and handle nulls based on the specified behavior.
        foreach (var dirtyValue in dirtyValues!)
        {
            // Get the cleaned value.
            var cleanValue = cleaner(dirtyValue);

            // If the cleaned value is not null, add it to the clean list.
            if (cleanValue is not null)
            {
                cleanList.Add(cleanValue);
                continue;
            }

            // If the cleaned value is null, handle according to the specified behavior.
            switch (valueNullBehavior)
            {
                // If we should remove nulls, do nothing (skip adding).
                default:
                case CleanValueNullBehavior.RemoveNulls:
                    break;

                // If we should throw on null, raise an exception.
                case CleanValueNullBehavior.ThrowOnNull:
                    throw new NullReferenceException("A cleaned value evaluated to null.");
            }
        }

        // After cleaning, if the list is empty, handle according to the specified behavior.
        if (cleanList.Falsey())
            return HandleCleanEmptyBehavior<T>(nameof(enumerable), enumEmptyBehavior);

        // Return the cleaned list.
        return cleanList;

        // Local function to handle empty enumerable behavior.
        static IEnumerable<T2>? HandleCleanEmptyBehavior<T2>(
            string paramName,
            CleanEnumEmptyBehavior enumEmptyBehavior)
        {
            return enumEmptyBehavior switch
            {
                CleanEnumEmptyBehavior.ReturnNull => null,
                CleanEnumEmptyBehavior.ReturnEmpty => [],
                CleanEnumEmptyBehavior.Throw => throw new ArgumentException(
                    "The enumerable is empty after cleaning.",
                    paramName),
                _ => null,
            };
        }
    }

    /// <summary>
    /// Behavior options for handling a null or empty enumerable during cleaning.
    /// </summary>
    public enum CleanEnumEmptyBehavior
    {
        ReturnEmpty,
        ReturnNull,
        Throw
    }

    /// <summary>
    /// Behavior options for handling null values during cleaning.
    /// </summary>
    public enum CleanValueNullBehavior
    {
        RemoveNulls,
        ThrowOnNull
    }
}

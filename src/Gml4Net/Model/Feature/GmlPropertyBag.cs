using System.Collections;

namespace Gml4Net.Model.Feature;

/// <summary>
/// A named feature property entry, preserving the original XML property order.
/// </summary>
public sealed class GmlPropertyEntry
{
    /// <summary>The property name.</summary>
    public required string Name { get; init; }

    /// <summary>The property value.</summary>
    public required GmlPropertyValue Value { get; init; }
}

/// <summary>
/// Ordered property bag with dictionary-style access for the first value of each name.
/// </summary>
public sealed class GmlPropertyBag : IReadOnlyDictionary<string, GmlPropertyValue>
{
    private readonly IReadOnlyList<GmlPropertyEntry> _entries;
    private readonly IReadOnlyDictionary<string, GmlPropertyValue> _lookup;

    /// <summary>An empty property bag.</summary>
    public static GmlPropertyBag Empty { get; } = new([]);

    /// <summary>
    /// Initializes a property bag from the given ordered property entries.
    /// </summary>
    /// <param name="entries">Ordered property entries.</param>
    public GmlPropertyBag(IEnumerable<GmlPropertyEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var entryList = entries.ToList().AsReadOnly();
        var lookup = new Dictionary<string, GmlPropertyValue>(StringComparer.Ordinal);

        foreach (var entry in entryList)
        {
            if (!lookup.ContainsKey(entry.Name))
                lookup[entry.Name] = entry.Value;
        }

        _entries = entryList;
        _lookup = lookup;
    }

    /// <summary>
    /// Ordered property entries, including repeated names.
    /// </summary>
    public IReadOnlyList<GmlPropertyEntry> Entries => _entries;

    /// <summary>
    /// Returns all values with the given property name in document order.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>All matching values.</returns>
    public IReadOnlyList<GmlPropertyValue> GetValues(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return _entries
            .Where(entry => string.Equals(entry.Name, name, StringComparison.Ordinal))
            .Select(entry => entry.Value)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public GmlPropertyValue this[string key] => _lookup[key];

    /// <inheritdoc />
    public IEnumerable<string> Keys => _lookup.Keys;

    /// <inheritdoc />
    public IEnumerable<GmlPropertyValue> Values => _lookup.Values;

    /// <inheritdoc />
    public int Count => _lookup.Count;

    /// <inheritdoc />
    public bool ContainsKey(string key) => _lookup.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out GmlPropertyValue value) => _lookup.TryGetValue(key, out value!);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, GmlPropertyValue>> GetEnumerator() => _lookup.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

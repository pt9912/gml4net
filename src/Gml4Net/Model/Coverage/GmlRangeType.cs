namespace Gml4Net.Model.Coverage;

/// <summary>
/// Coverage range type defining the structure of range set values.
/// </summary>
public sealed class GmlRangeType
{
    /// <summary>Field definitions.</summary>
    public IReadOnlyList<GmlRangeField> Fields { get; init; } = [];
}

/// <summary>
/// A single field (band) in a range type definition.
/// </summary>
public sealed class GmlRangeField
{
    /// <summary>Field name.</summary>
    public required string Name { get; init; }

    /// <summary>Field description.</summary>
    public string? Description { get; init; }

    /// <summary>Unit of measure.</summary>
    public string? Uom { get; init; }
}

namespace Gml4Net.Model.Coverage;

/// <summary>
/// Coverage range set containing data values or a file reference.
/// </summary>
public sealed class GmlRangeSet
{
    /// <summary>Inline data values (tuple list).</summary>
    public string? DataBlock { get; init; }

    /// <summary>External file reference.</summary>
    public string? FileReference { get; init; }
}

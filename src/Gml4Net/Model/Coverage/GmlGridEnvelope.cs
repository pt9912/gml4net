namespace Gml4Net.Model.Coverage;

/// <summary>
/// Grid index bounds (low and high corner).
/// </summary>
public sealed class GmlGridEnvelope
{
    /// <summary>Low index values.</summary>
    public required IReadOnlyList<int> Low { get; init; }

    /// <summary>High index values.</summary>
    public required IReadOnlyList<int> High { get; init; }
}

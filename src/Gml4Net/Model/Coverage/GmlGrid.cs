namespace Gml4Net.Model.Coverage;

/// <summary>
/// A GML Grid with dimension, limits and axis labels.
/// </summary>
public class GmlGrid
{
    /// <summary>Number of dimensions.</summary>
    public required int Dimension { get; init; }

    /// <summary>Grid limits (low/high indices).</summary>
    public required GmlGridEnvelope Limits { get; init; }

    /// <summary>Axis labels.</summary>
    public IReadOnlyList<string> AxisLabels { get; init; } = [];
}

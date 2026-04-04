namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML 3.x Curve composed of one or more segments.
/// Segments are flattened to a single coordinate list.
/// </summary>
public sealed class GmlCurve : GmlGeometry
{
    /// <summary>Flattened coordinates from all curve segments.</summary>
    public required IReadOnlyList<GmlCoordinate> Coordinates { get; init; }
}

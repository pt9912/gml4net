namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML LinearRing (closed ring used as polygon boundary).
/// </summary>
public sealed class GmlLinearRing : GmlGeometry
{
    /// <summary>Ordered list of coordinates forming the closed ring.</summary>
    public required IReadOnlyList<GmlCoordinate> Coordinates { get; init; }
}

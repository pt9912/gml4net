namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML LineString geometry.
/// </summary>
public sealed class GmlLineString : GmlGeometry
{
    /// <summary>Ordered list of coordinates forming the line.</summary>
    public required IReadOnlyList<GmlCoordinate> Coordinates { get; init; }
}

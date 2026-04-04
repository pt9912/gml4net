namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML MultiPolygon geometry.
/// </summary>
public sealed class GmlMultiPolygon : GmlGeometry
{
    /// <summary>Polygon members.</summary>
    public required IReadOnlyList<GmlPolygon> Polygons { get; init; }
}

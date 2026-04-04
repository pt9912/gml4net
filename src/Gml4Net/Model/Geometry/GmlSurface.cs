namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML 3.x Surface composed of polygon patches.
/// </summary>
public sealed class GmlSurface : GmlGeometry
{
    /// <summary>Polygon patches making up the surface.</summary>
    public required IReadOnlyList<GmlPolygon> Patches { get; init; }
}

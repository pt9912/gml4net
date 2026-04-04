namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML Polygon with an exterior ring and optional interior rings (holes).
/// </summary>
public sealed class GmlPolygon : GmlGeometry
{
    /// <summary>Exterior boundary ring.</summary>
    public required GmlLinearRing Exterior { get; init; }

    /// <summary>Interior boundary rings (holes).</summary>
    public IReadOnlyList<GmlLinearRing> Interior { get; init; } = [];
}

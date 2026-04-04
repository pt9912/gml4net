namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML Point geometry with a single coordinate.
/// </summary>
public sealed class GmlPoint : GmlGeometry
{
    /// <summary>The point coordinate.</summary>
    public required GmlCoordinate Coordinate { get; init; }
}

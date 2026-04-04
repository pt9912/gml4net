namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML 2.1.2 Box (bounding box, replaced by Envelope in GML 3).
/// </summary>
public sealed class GmlBox : GmlGeometry
{
    /// <summary>Lower corner coordinate (minimum values).</summary>
    public required GmlCoordinate LowerCorner { get; init; }

    /// <summary>Upper corner coordinate (maximum values).</summary>
    public required GmlCoordinate UpperCorner { get; init; }
}

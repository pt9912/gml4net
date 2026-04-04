namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML 3.x Envelope (bounding box defined by lower and upper corners).
/// </summary>
public sealed class GmlEnvelope : GmlGeometry
{
    /// <summary>Lower corner coordinate (minimum values).</summary>
    public required GmlCoordinate LowerCorner { get; init; }

    /// <summary>Upper corner coordinate (maximum values).</summary>
    public required GmlCoordinate UpperCorner { get; init; }
}

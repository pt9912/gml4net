namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML MultiPoint geometry.
/// </summary>
public sealed class GmlMultiPoint : GmlGeometry
{
    /// <summary>Point members.</summary>
    public required IReadOnlyList<GmlPoint> Points { get; init; }
}

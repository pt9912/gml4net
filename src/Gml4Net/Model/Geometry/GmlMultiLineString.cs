namespace Gml4Net.Model.Geometry;

/// <summary>
/// A GML MultiLineString geometry.
/// </summary>
public sealed class GmlMultiLineString : GmlGeometry
{
    /// <summary>LineString members.</summary>
    public required IReadOnlyList<GmlLineString> LineStrings { get; init; }
}

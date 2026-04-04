namespace Gml4Net.Model.Geometry;

/// <summary>
/// Abstract base class for all GML geometry types.
/// </summary>
public abstract class GmlGeometry : GmlNode, IGmlRootContent
{
    internal GmlGeometry() { }

    /// <summary>GML version this geometry was parsed from.</summary>
    public GmlVersion? Version { get; init; }

    /// <summary>Spatial reference system name (e.g. "EPSG:4326").</summary>
    public string? SrsName { get; init; }
}

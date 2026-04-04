using Gml4Net.Model.Geometry;

namespace Gml4Net.Model;

/// <summary>
/// A parsed GML document containing the root content and metadata.
/// </summary>
public sealed class GmlDocument
{
    /// <summary>Detected GML version.</summary>
    public required GmlVersion Version { get; init; }

    /// <summary>Root content (geometry, feature, feature collection, or coverage).</summary>
    public required IGmlRootContent Root { get; init; }

    /// <summary>Optional bounding box from gml:boundedBy.</summary>
    public GmlEnvelope? BoundedBy { get; init; }
}

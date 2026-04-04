using Gml4Net.Model.Geometry;

namespace Gml4Net.Model.Feature;

/// <summary>
/// A collection of GML features, typically from a WFS response.
/// </summary>
public sealed class GmlFeatureCollection : GmlNode, IGmlRootContent
{
    /// <summary>Features in the collection.</summary>
    public IReadOnlyList<GmlFeature> Features { get; init; } = [];

    /// <summary>Optional bounding box.</summary>
    public GmlEnvelope? BoundedBy { get; init; }
}

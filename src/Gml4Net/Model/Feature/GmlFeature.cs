namespace Gml4Net.Model.Feature;

/// <summary>
/// A GML feature with an optional ID and typed properties.
/// </summary>
public sealed class GmlFeature : GmlNode, IGmlRootContent
{
    /// <summary>Optional feature identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Feature properties in document order.</summary>
    public GmlPropertyBag Properties { get; init; } = GmlPropertyBag.Empty;
}

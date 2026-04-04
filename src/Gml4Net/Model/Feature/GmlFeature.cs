namespace Gml4Net.Model.Feature;

/// <summary>
/// A GML feature with an optional ID and typed properties.
/// </summary>
public sealed class GmlFeature : GmlNode, IGmlRootContent
{
    /// <summary>Optional feature identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Feature properties keyed by name.</summary>
    public IReadOnlyDictionary<string, GmlPropertyValue> Properties { get; init; }
        = new Dictionary<string, GmlPropertyValue>();
}

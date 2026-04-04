namespace Gml4Net.Model;

/// <summary>
/// Represents an XML element that could not be mapped to a known GML type.
/// Prevents silent data loss for unrecognized elements.
/// </summary>
public sealed class GmlUnsupportedNode : GmlNode
{
    /// <summary>Local name of the unrecognized element.</summary>
    public required string ElementName { get; init; }

    /// <summary>Namespace URI of the unrecognized element.</summary>
    public string? NamespaceUri { get; init; }

    /// <summary>Raw XML content of the element.</summary>
    public string? RawXml { get; init; }
}

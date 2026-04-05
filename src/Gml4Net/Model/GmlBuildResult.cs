namespace Gml4Net.Model;

/// <summary>
/// Result of a combined parse-and-convert operation. Contains the builder output
/// for the detected root type, the parsed document for metadata access, and any
/// diagnostic issues encountered during parsing.
/// </summary>
/// <typeparam name="TGeometry">Output type for geometry conversions.</typeparam>
/// <typeparam name="TFeature">Output type for feature conversions.</typeparam>
/// <typeparam name="TCollection">Output type for feature collection conversions.</typeparam>
public sealed class GmlBuildResult<TGeometry, TFeature, TCollection>
{
    /// <summary>Set when the root is a geometry.</summary>
    public TGeometry? Geometry { get; init; }

    /// <summary>Set when the root is a feature.</summary>
    public TFeature? Feature { get; init; }

    /// <summary>Set when the root is a feature collection.</summary>
    public TCollection? Collection { get; init; }

    /// <summary>Set when the root is a coverage.</summary>
    public TFeature? Coverage { get; init; }

    /// <summary>The parsed document (for metadata such as version and bounding box).</summary>
    public GmlDocument? Document { get; init; }

    /// <summary>Diagnostic issues encountered during parsing.</summary>
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];

    /// <summary>True if any issue has Error severity.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == GmlIssueSeverity.Error);
}

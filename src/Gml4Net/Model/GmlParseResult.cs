namespace Gml4Net.Model;

/// <summary>
/// Result of a GML parse operation. Contains the parsed document (if successful)
/// and any diagnostic issues encountered during parsing.
/// </summary>
public sealed class GmlParseResult
{
    /// <summary>The parsed document, or null if a fatal error occurred.</summary>
    public GmlDocument? Document { get; init; }

    /// <summary>Diagnostic issues encountered during parsing.</summary>
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];

    /// <summary>True if any issue has Error severity.</summary>
    public bool HasErrors => Issues.Any(i => i.Severity == GmlIssueSeverity.Error);
}

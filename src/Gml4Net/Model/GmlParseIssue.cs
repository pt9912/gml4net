namespace Gml4Net.Model;

/// <summary>
/// Severity levels for parse issues.
/// </summary>
public enum GmlIssueSeverity
{
    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Non-fatal issue.</summary>
    Warning,

    /// <summary>Fatal parse error.</summary>
    Error
}

/// <summary>
/// A diagnostic issue produced during GML parsing.
/// </summary>
public sealed class GmlParseIssue
{
    /// <summary>Issue severity.</summary>
    public required GmlIssueSeverity Severity { get; init; }

    /// <summary>Machine-readable issue code (e.g. "missing_coordinates").</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable description.</summary>
    public required string Message { get; init; }

    /// <summary>Optional location (XPath or element name).</summary>
    public string? Location { get; init; }
}

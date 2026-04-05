using Gml4Net.Model;

namespace Gml4Net.Parser;

/// <summary>
/// Structured error information for a single feature failure during streaming.
/// </summary>
public sealed class StreamingError
{
    /// <summary>Exception thrown during parsing or handling, if any.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Parser-level diagnostic issues, if any.</summary>
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];

    /// <summary>The feature ID, if it was known before the error occurred.</summary>
    public string? FeatureId { get; init; }

    /// <summary>
    /// True if the stream can continue past this error (recoverable).
    /// False for fatal errors that prevent further reading.
    /// </summary>
    public bool CanContinue { get; init; }
}

using Gml4Net.Model;
using Gml4Net.Model.Feature;

namespace Gml4Net.Parser.Streaming;

/// <summary>
/// Internal result type for a single feature parse attempt in the streaming path.
/// Carries either a successfully parsed feature or error information.
/// </summary>
internal sealed record FeatureStreamItem
{
    /// <summary>The parsed feature, or null if parsing failed.</summary>
    public GmlFeature? Feature { get; init; }

    /// <summary>Diagnostic issues encountered during parsing.</summary>
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];

    /// <summary>Exception thrown during parsing or handling, if any.</summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// True if the stream can continue after this item (recoverable error).
    /// False for fatal errors that prevent further reading.
    /// </summary>
    public bool CanContinue { get; init; }

    // "Success" means "a feature was produced", not "no diagnostics exist".
    public bool IsSuccess => Feature is not null;
}

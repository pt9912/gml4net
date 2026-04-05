namespace Gml4Net.Parser;

/// <summary>
/// Options for the streaming GML parser.
/// </summary>
public sealed class StreamingParserOptions
{
    /// <summary>
    /// Behavior when parsing or processing a single feature fails.
    /// Default: continue and emit error callback.
    /// </summary>
    public StreamingErrorBehavior ErrorBehavior { get; init; }
        = StreamingErrorBehavior.Continue;

    /// <summary>
    /// Optional progress reporting after each feature outcome
    /// (success or failure).
    /// </summary>
    public IProgress<StreamingProgress>? Progress { get; init; }
}

/// <summary>
/// Controls whether the streaming parser stops or continues after a feature error.
/// </summary>
public enum StreamingErrorBehavior
{
    /// <summary>Stop parsing after the first feature error.</summary>
    Stop,

    /// <summary>Continue parsing and report errors via the error callback.</summary>
    Continue
}

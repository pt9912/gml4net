using Gml4Net.Model.Feature;

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
    /// (success, failure, or filtered).
    /// </summary>
    public IProgress<StreamingProgress>? Progress { get; init; }

    /// <summary>
    /// Optional predicate that determines whether a successfully parsed feature
    /// should be emitted to the configured callback, batch, or sink.
    /// Features for which the predicate returns false are silently skipped
    /// and counted as filtered, not as processed or failed. If the parsed feature
    /// already carries non-fatal parser diagnostics, these diagnostics are still
    /// forwarded via the configured error callback.
    /// </summary>
    public Func<GmlFeature, bool>? Filter { get; init; }
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

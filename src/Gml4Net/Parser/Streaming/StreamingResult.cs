namespace Gml4Net.Parser;

/// <summary>
/// Result of a streaming parse operation, containing feature counters.
/// </summary>
public sealed record StreamingResult
{
    /// <summary>
    /// Number of features that were successfully processed and emitted to the
    /// configured callback, batch, or sink.
    /// </summary>
    public int FeaturesProcessed { get; init; }

    /// <summary>
    /// Number of features that failed during parsing or downstream handling,
    /// or were lost due to premature termination before delivery.
    /// </summary>
    public int FeaturesFailed { get; init; }

    /// <summary>
    /// Number of features that were successfully parsed but excluded by the
    /// configured filter predicate.
    /// </summary>
    public int FeaturesFiltered { get; init; }
}

/// <summary>
/// Progress snapshot reported after each feature outcome during streaming.
/// </summary>
/// <param name="FeaturesProcessed">Cumulative count of successfully processed features.</param>
/// <param name="FeaturesFailed">Cumulative count of failed features.</param>
/// <param name="FeaturesFiltered">Cumulative count of filtered features.</param>
public readonly record struct StreamingProgress(
    int FeaturesProcessed,
    int FeaturesFailed,
    int FeaturesFiltered);

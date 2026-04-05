using Gml4Net.Model.Feature;

namespace Gml4Net.Interop;

/// <summary>
/// Contract for components that consume parsed GML features during streaming,
/// e.g. database inserts or file writes.
/// </summary>
public interface IFeatureSink
{
    /// <summary>
    /// Writes a single successfully parsed feature.
    /// Called exactly once per feature.
    /// </summary>
    /// <param name="feature">The parsed GML feature.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask WriteFeatureAsync(
        GmlFeature feature,
        CancellationToken ct = default);

    /// <summary>
    /// Signals that all features have been written successfully.
    /// Called exactly once at the successful end of parsing.
    /// Not called on fatal abort or cancellation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    ValueTask CompleteAsync(CancellationToken ct = default);
}

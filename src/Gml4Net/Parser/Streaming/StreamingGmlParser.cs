using System.Runtime.ExceptionServices;
using Gml4Net.Model.Feature;
using Gml4Net.Parser.Streaming;

namespace Gml4Net.Parser;

/// <summary>
/// Public streaming parser for large GML/WFS feature collections.
/// Streams one feature at a time and invokes registered handlers immediately.
/// </summary>
/// <remarks>
/// Each instance is intended for exactly one parse run. Do not reuse instances.
/// </remarks>
public sealed class StreamingGmlParser
{
    private readonly StreamingParserOptions _options;
    private Func<GmlFeature, ValueTask>? _onFeature;
    private Action<StreamingError>? _onError;
    private Action<StreamingResult>? _onEnd;

    /// <summary>
    /// Creates a new streaming parser with the specified options.
    /// </summary>
    /// <param name="options">Parser options, or null for defaults.</param>
    public StreamingGmlParser(StreamingParserOptions? options = null)
    {
        _options = options ?? new StreamingParserOptions();
    }

    /// <summary>
    /// Registers a callback invoked immediately for each successfully parsed feature.
    /// </summary>
    /// <param name="callback">The feature handler.</param>
    /// <returns>This parser instance for fluent chaining.</returns>
    public StreamingGmlParser OnFeature(Func<GmlFeature, ValueTask> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _onFeature = callback;
        return this;
    }

    /// <summary>
    /// Registers a callback invoked for each feature-level error.
    /// </summary>
    /// <param name="callback">The error handler.</param>
    /// <returns>This parser instance for fluent chaining.</returns>
    public StreamingGmlParser OnError(Action<StreamingError> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _onError = callback;
        return this;
    }

    /// <summary>
    /// Registers a callback invoked exactly once when parsing ends, including
    /// on fatal abort or cancellation.
    /// </summary>
    /// <param name="callback">The end handler.</param>
    /// <returns>This parser instance for fluent chaining.</returns>
    public StreamingGmlParser OnEnd(Action<StreamingResult> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _onEnd = callback;
        return this;
    }

    /// <summary>
    /// Parses features from the given stream, invoking registered callbacks.
    /// </summary>
    /// <param name="stream">The input stream containing the GML/WFS XML document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing feature counters.</returns>
    public async Task<StreamingResult> ParseAsync(
        Stream stream,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        int processed = 0;
        int failed = 0;
        ExceptionDispatchInfo? fatalEdi = null;

        try
        {
            await foreach (var item in GmlFeatureStreamParser.ParseItemsAsync(stream, ct).ConfigureAwait(false))
            {
                if (item.IsSuccess)
                {
                    if (_onFeature is not null)
                    {
                        try
                        {
                            await _onFeature(item.Feature!).ConfigureAwait(false);
                            processed++;
                            _options.Progress?.Report(new StreamingProgress(processed, failed));
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            failed++;
                            _onError?.Invoke(new StreamingError
                            {
                                Exception = ex,
                                Issues = item.Issues,
                                FeatureId = item.Feature!.Id,
                                CanContinue = true
                            });

                            _options.Progress?.Report(new StreamingProgress(processed, failed));

                            if (_options.ErrorBehavior == StreamingErrorBehavior.Stop)
                                break;
                        }
                    }
                    else
                    {
                        processed++;
                        _options.Progress?.Report(new StreamingProgress(processed, failed));
                    }
                }
                else
                {
                    failed++;
                    _onError?.Invoke(new StreamingError
                    {
                        Exception = item.Exception,
                        Issues = item.Issues,
                        CanContinue = item.CanContinue
                    });

                    _options.Progress?.Report(new StreamingProgress(processed, failed));

                    if (!item.CanContinue || _options.ErrorBehavior == StreamingErrorBehavior.Stop)
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            fatalEdi = ExceptionDispatchInfo.Capture(ex);
        }

        var result = new StreamingResult { FeaturesProcessed = processed, FeaturesFailed = failed };
        _onEnd?.Invoke(result);

        fatalEdi?.Throw();
        return result;
    }
}

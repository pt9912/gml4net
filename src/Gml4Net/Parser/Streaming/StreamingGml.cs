using Gml4Net.Interop;
using Gml4Net.Model.Feature;
using Gml4Net.Parser.Streaming;

namespace Gml4Net.Parser;

/// <summary>
/// Convenience entry points for streaming GML parsing with builder integration
/// or feature sinks.
/// </summary>
public static class StreamingGml
{
    /// <summary>
    /// Streams features from a GML/WFS document, transforming each via the
    /// given builder and invoking <paramref name="onFeature"/> per result.
    /// </summary>
    /// <typeparam name="TGeometry">Builder geometry output type.</typeparam>
    /// <typeparam name="TFeature">Builder feature output type.</typeparam>
    /// <typeparam name="TCollection">Builder collection output type.</typeparam>
    /// <param name="stream">The input stream.</param>
    /// <param name="builder">The builder for feature transformation.</param>
    /// <param name="onFeature">Callback per transformed feature.</param>
    /// <param name="onError">Optional callback for recoverable errors.</param>
    /// <param name="options">Parser options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing feature counters.</returns>
    public static async Task<StreamingResult> ParseAsync<TGeometry, TFeature, TCollection>(
        Stream stream,
        IBuilder<TGeometry, TFeature, TCollection> builder,
        Func<TFeature, ValueTask> onFeature,
        Action<StreamingError>? onError = null,
        StreamingParserOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(onFeature);

        var parser = new StreamingGmlParser(options);

        parser.OnFeature(async feature =>
        {
            var built = builder.BuildFeature(feature);
            await onFeature(built).ConfigureAwait(false);
        });

        if (onError is not null)
            parser.OnError(onError);

        return await parser.ParseAsync(stream, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Streams features from a GML/WFS document in batches, transforming each
    /// via the given builder and invoking <paramref name="onBatch"/> per batch.
    /// </summary>
    /// <typeparam name="TGeometry">Builder geometry output type.</typeparam>
    /// <typeparam name="TFeature">Builder feature output type.</typeparam>
    /// <typeparam name="TCollection">Builder collection output type.</typeparam>
    /// <param name="stream">The input stream.</param>
    /// <param name="builder">The builder for feature transformation.</param>
    /// <param name="onBatch">Callback per batch of transformed features.</param>
    /// <param name="batchSize">Number of features per batch.</param>
    /// <param name="onError">Optional callback for recoverable errors.</param>
    /// <param name="options">Parser options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing feature counters.</returns>
    public static async Task<StreamingResult> ParseBatchesAsync<TGeometry, TFeature, TCollection>(
        Stream stream,
        IBuilder<TGeometry, TFeature, TCollection> builder,
        Func<IReadOnlyList<TFeature>, ValueTask> onBatch,
        int batchSize = 100,
        Action<StreamingError>? onError = null,
        StreamingParserOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(onBatch);

        if (batchSize < 1)
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be at least 1.");

        var opts = options ?? new StreamingParserOptions();
        int processed = 0;
        int failed = 0;
        var batch = new List<TFeature>(batchSize);
        var stopped = false;

        await foreach (var item in GmlFeatureStreamParser.ParseItemsAsync(stream, ct).ConfigureAwait(false))
        {
            if (item.IsSuccess)
            {
                try
                {
                    var built = builder.BuildFeature(item.Feature!);
                    batch.Add(built);
                }
                catch (Exception ex)
                {
                    failed++;
                    onError?.Invoke(new StreamingError
                    {
                        Exception = ex,
                        Issues = item.Issues,
                        FeatureId = item.Feature!.Id,
                        CanContinue = true
                    });

                    if (opts.ErrorBehavior == StreamingErrorBehavior.Stop)
                    {
                        stopped = true;
                        break;
                    }

                    opts.Progress?.Report(new StreamingProgress(processed, failed));
                    continue;
                }

                if (batch.Count >= batchSize)
                {
                    try
                    {
                        await onBatch(batch).ConfigureAwait(false);
                        processed += batch.Count;
                    }
                    catch (Exception ex)
                    {
                        failed += batch.Count;
                        onError?.Invoke(new StreamingError
                        {
                            Exception = ex,
                            CanContinue = true
                        });

                        if (opts.ErrorBehavior == StreamingErrorBehavior.Stop)
                        {
                            batch.Clear();
                            stopped = true;
                            break;
                        }
                    }

                    batch.Clear();
                }

                opts.Progress?.Report(new StreamingProgress(processed, failed));
            }
            else
            {
                failed++;
                onError?.Invoke(new StreamingError
                {
                    Exception = item.Exception,
                    Issues = item.Issues,
                    CanContinue = item.CanContinue
                });

                opts.Progress?.Report(new StreamingProgress(processed, failed));

                if (!item.CanContinue || opts.ErrorBehavior == StreamingErrorBehavior.Stop)
                {
                    stopped = true;
                    break;
                }
            }
        }

        // Flush or count remaining batch
        if (batch.Count > 0)
        {
            if (stopped)
            {
                // Pending features lost due to premature termination
                failed += batch.Count;
            }
            else
            {
                try
                {
                    await onBatch(batch).ConfigureAwait(false);
                    processed += batch.Count;
                }
                catch (Exception ex)
                {
                    failed += batch.Count;
                    onError?.Invoke(new StreamingError
                    {
                        Exception = ex,
                        CanContinue = true
                    });
                }
            }
        }

        return new StreamingResult { FeaturesProcessed = processed, FeaturesFailed = failed };
    }

    /// <summary>
    /// Streams features from a GML/WFS document into a feature sink.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="sink">The sink that receives each parsed feature.</param>
    /// <param name="onError">Optional callback for recoverable errors.</param>
    /// <param name="options">Parser options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing feature counters.</returns>
    public static async Task<StreamingResult> ParseAsync(
        Stream stream,
        IFeatureSink sink,
        Action<StreamingError>? onError = null,
        StreamingParserOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(sink);

        var opts = options ?? new StreamingParserOptions();
        int processed = 0;
        int failed = 0;
        var stopped = false;

        await foreach (var item in GmlFeatureStreamParser.ParseItemsAsync(stream, ct).ConfigureAwait(false))
        {
            if (item.IsSuccess)
            {
                try
                {
                    await sink.WriteFeatureAsync(item.Feature!, ct).ConfigureAwait(false);
                    processed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    onError?.Invoke(new StreamingError
                    {
                        Exception = ex,
                        Issues = item.Issues,
                        FeatureId = item.Feature!.Id,
                        CanContinue = true
                    });

                    if (opts.ErrorBehavior == StreamingErrorBehavior.Stop)
                    {
                        stopped = true;
                        break;
                    }
                }
            }
            else
            {
                failed++;
                onError?.Invoke(new StreamingError
                {
                    Exception = item.Exception,
                    Issues = item.Issues,
                    CanContinue = item.CanContinue
                });

                if (!item.CanContinue || opts.ErrorBehavior == StreamingErrorBehavior.Stop)
                {
                    stopped = true;
                    break;
                }
            }

            opts.Progress?.Report(new StreamingProgress(processed, failed));
        }

        if (!stopped)
            await sink.CompleteAsync(ct).ConfigureAwait(false);

        return new StreamingResult { FeaturesProcessed = processed, FeaturesFailed = failed };
    }
}

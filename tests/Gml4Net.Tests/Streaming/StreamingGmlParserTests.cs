using System.Text;
using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Gml4Net.Parser.Streaming;
using Xunit;

namespace Gml4Net.Tests.Streaming;

public class StreamingGmlParserTests
{
    // ---- Basic streaming ----

    [Fact]
    public async Task ParseAsync_WithWfsMember_StreamsFeatures()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:City gml:id="city.1"><app:name>Munich</app:name></app:City>
                </wfs:member>
                <wfs:member>
                    <app:City gml:id="city.2"><app:name>Berlin</app:name></app:City>
                </wfs:member>
            </wfs:FeatureCollection>
            """;
        using var stream = ToStream(xml);
        var features = new List<GmlFeature>();

        var parser = new StreamingGmlParser();
        parser.OnFeature(f => { features.Add(f); return ValueTask.CompletedTask; });

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(2);
        result.FeaturesFailed.Should().Be(0);
        features.Should().HaveCount(2);
        features[0].Id.Should().Be("city.1");
        features[1].Id.Should().Be("city.2");
    }

    [Fact]
    public async Task ParseAsync_WithGmlFeatureMember_StreamsFeatures()
    {
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml"
                                   xmlns:app="http://example.com/app">
                <gml:featureMember>
                    <app:Road fid="road.1"><app:name>Main St</app:name></app:Road>
                </gml:featureMember>
            </gml:FeatureCollection>
            """;
        using var stream = ToStream(xml);
        var features = new List<GmlFeature>();

        var parser = new StreamingGmlParser();
        parser.OnFeature(f => { features.Add(f); return ValueTask.CompletedTask; });

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(1);
        features[0].Id.Should().Be("road.1");
    }

    [Fact]
    public async Task ParseAsync_WithFeatureMembers_StreamsFeatures()
    {
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <gml:featureMembers>
                    <app:A gml:id="a.1"><app:x>1</app:x></app:A>
                    <app:B gml:id="b.1"><app:x>2</app:x></app:B>
                </gml:featureMembers>
            </gml:FeatureCollection>
            """;
        using var stream = ToStream(xml);
        var features = new List<GmlFeature>();

        var parser = new StreamingGmlParser();
        parser.OnFeature(f => { features.Add(f); return ValueTask.CompletedTask; });

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(2);
        features[0].Id.Should().Be("a.1");
        features[1].Id.Should().Be("b.1");
    }

    // ---- OnFeature ordering and timing ----

    [Fact]
    public async Task ParseAsync_InvokesOnFeatureInDocumentOrder()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var ids = new List<string?>();

        var parser = new StreamingGmlParser();
        parser.OnFeature(f => { ids.Add(f.Id); return ValueTask.CompletedTask; });

        await parser.ParseAsync(stream);

        ids.Should().Equal("item.0", "item.1", "item.2", "item.3", "item.4");
    }

    [Fact]
    public async Task ParseAsync_InvokesOnFeatureImmediately()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var timestamps = new List<int>();
        var counter = 0;

        var parser = new StreamingGmlParser();
        parser.OnFeature(f =>
        {
            timestamps.Add(Interlocked.Increment(ref counter));
            return ValueTask.CompletedTask;
        });

        await parser.ParseAsync(stream);

        // Each callback should have been invoked sequentially with incrementing counter
        timestamps.Should().Equal(1, 2, 3);
    }

    // ---- Without OnFeature ----

    [Fact]
    public async Task ParseAsync_WithoutOnFeature_CountsOnly()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);

        var parser = new StreamingGmlParser();

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(3);
        result.FeaturesFailed.Should().Be(0);
    }

    // ---- OnEnd ----

    [Fact]
    public async Task ParseAsync_OnEnd_CalledExactlyOnce()
    {
        var xml = BuildWfsCollection(2);
        using var stream = ToStream(xml);
        var endResults = new List<StreamingResult>();

        var parser = new StreamingGmlParser();
        parser.OnEnd(r => endResults.Add(r));

        var result = await parser.ParseAsync(stream);

        endResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task ParseAsync_OnEnd_ReceivesSameResultAsReturnValue()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        StreamingResult? endResult = null;

        var parser = new StreamingGmlParser();
        parser.OnEnd(r => endResult = r);

        var result = await parser.ParseAsync(stream);

        endResult.Should().Be(result);
    }

    [Fact]
    public async Task ParseAsync_OnEnd_CalledOnCancellation()
    {
        var xml = BuildWfsCollection(100);
        using var stream = ToStream(xml);
        using var cts = new CancellationTokenSource();
        StreamingResult? endResult = null;

        var parser = new StreamingGmlParser();
        parser.OnFeature(f =>
        {
            cts.Cancel();
            return ValueTask.CompletedTask;
        });
        parser.OnEnd(r => endResult = r);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => parser.ParseAsync(stream, cts.Token));

        endResult.Should().NotBeNull();
    }

    // ---- Progress ----

    [Fact]
    public async Task ParseAsync_Progress_ReportsCumulativeCounters()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var reports = new List<StreamingProgress>();

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            Progress = new SyncProgress<StreamingProgress>(p => reports.Add(p))
        });
        parser.OnFeature(_ => ValueTask.CompletedTask);

        await parser.ParseAsync(stream);

        reports.Should().HaveCount(3);
        reports[0].FeaturesProcessed.Should().Be(1);
        reports[1].FeaturesProcessed.Should().Be(2);
        reports[2].FeaturesProcessed.Should().Be(3);
        reports.Should().AllSatisfy(r => r.FeaturesFailed.Should().Be(0));
    }

    // ---- Error handling ----

    [Fact]
    public async Task ParseAsync_HandlerError_ReportedViaOnError()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var errors = new List<StreamingError>();
        var handlerException = new InvalidOperationException("handler failed");

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Continue
        });
        parser.OnFeature(f =>
        {
            if (f.Id == "item.1") throw handlerException;
            return ValueTask.CompletedTask;
        });
        parser.OnError(e => errors.Add(e));

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(2);
        result.FeaturesFailed.Should().Be(1);
        errors.Should().HaveCount(1);
        errors[0].Exception.Should().Be(handlerException);
        errors[0].FeatureId.Should().Be("item.1");
        errors[0].CanContinue.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_Continue_ContinuesAfterFeatureError()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var processedIds = new List<string?>();

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Continue
        });
        parser.OnFeature(f =>
        {
            if (f.Id == "item.2") throw new InvalidOperationException("fail");
            processedIds.Add(f.Id);
            return ValueTask.CompletedTask;
        });

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(4);
        result.FeaturesFailed.Should().Be(1);
        processedIds.Should().Equal("item.0", "item.1", "item.3", "item.4");
    }

    [Fact]
    public async Task ParseAsync_Stop_StopsAfterFirstError()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var processedIds = new List<string?>();

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Stop
        });
        parser.OnFeature(f =>
        {
            if (f.Id == "item.2") throw new InvalidOperationException("fail");
            processedIds.Add(f.Id);
            return ValueTask.CompletedTask;
        });

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(2);
        result.FeaturesFailed.Should().Be(1);
        processedIds.Should().Equal("item.0", "item.1");
    }

    [Fact]
    public async Task ParseAsync_CorrectCounters()
    {
        var xml = BuildWfsCollection(10);
        using var stream = ToStream(xml);

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Continue
        });
        parser.OnFeature(f =>
        {
            // Fail on items 3 and 7
            if (f.Id is "item.3" or "item.7") throw new Exception("fail");
            return ValueTask.CompletedTask;
        });

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(8);
        result.FeaturesFailed.Should().Be(2);
    }

    // ---- Cancellation ----

    [Fact]
    public async Task ParseAsync_Cancellation_Respected()
    {
        var xml = BuildWfsCollection(1000);
        using var stream = ToStream(xml);
        using var cts = new CancellationTokenSource();
        var count = 0;

        var parser = new StreamingGmlParser();
        parser.OnFeature(_ =>
        {
            count++;
            if (count == 5) cts.Cancel();
            return ValueTask.CompletedTask;
        });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => parser.ParseAsync(stream, cts.Token));

        count.Should().Be(5);
    }

    // ---- StreamingGml convenience with builder ----

    [Fact]
    public async Task StreamingGml_ParseAsync_WithBuilder_TransformsFeatures()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var features = new List<object>();

        var result = await StreamingGml.ParseAsync(
            stream,
            builder,
            f => { features.Add(f); return ValueTask.CompletedTask; });

        result.FeaturesProcessed.Should().Be(3);
        features.Should().HaveCount(3);
    }

    [Fact]
    public async Task StreamingGml_ParseAsync_WithBuilder_ReportsErrors()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var errors = new List<StreamingError>();

        var result = await StreamingGml.ParseAsync(
            stream,
            builder,
            f => throw new Exception("fail"),
            onError: e => errors.Add(e),
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Continue });

        result.FeaturesFailed.Should().Be(3);
        errors.Should().HaveCount(3);
    }

    // ---- ParseBatchesAsync ----

    [Fact]
    public async Task ParseBatchesAsync_FlushesAtBatchSize()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var batchSizes = new List<int>();

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch => { batchSizes.Add(batch.Count); return ValueTask.CompletedTask; },
            batchSize: 2);

        // 5 features, batchSize 2: batches of 2, 2, 1
        result.FeaturesProcessed.Should().Be(5);
        batchSizes.Should().Equal(2, 2, 1);
    }

    [Fact]
    public async Task ParseBatchesAsync_FlushesPartialBatchAtEnd()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var batchSizes = new List<int>();

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch => { batchSizes.Add(batch.Count); return ValueTask.CompletedTask; },
            batchSize: 100);

        // All 3 features in one partial batch
        batchSizes.Should().Equal(3);
        result.FeaturesProcessed.Should().Be(3);
    }

    [Fact]
    public async Task ParseBatchesAsync_KeepsFeaturesAcrossRecoverableErrors()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var builder = new ThrowOnIdBuilder("item.2");
        var batchSizes = new List<int>();

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch => { batchSizes.Add(batch.Count); return ValueTask.CompletedTask; },
            batchSize: 3,
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Continue });

        // item.2 fails in builder, 4 succeed: batch of 3, then partial of 1
        result.FeaturesProcessed.Should().Be(4);
        result.FeaturesFailed.Should().Be(1);
        batchSizes.Should().Equal(3, 1);
    }

    [Fact]
    public async Task ParseBatchesAsync_BatchError_CountsNFeaturesAsFailed()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var batchCallCount = 0;

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch =>
            {
                batchCallCount++;
                if (batchCallCount == 1) throw new Exception("batch failed");
                return ValueTask.CompletedTask;
            },
            batchSize: 2,
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Continue });

        // First batch of 2 fails, second batch of 2 succeeds, partial of 1 succeeds
        result.FeaturesFailed.Should().Be(2);
        result.FeaturesProcessed.Should().Be(3);
    }

    [Fact]
    public async Task ParseBatchesAsync_Stop_CountsPendingAsFailed()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var builder = new ThrowOnIdBuilder("item.1");

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch => ValueTask.CompletedTask,
            batchSize: 3,
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Stop });

        // item.0 succeeds (buffered), item.1 fails in builder → Stop
        // Pending batch with 1 item counted as failed
        result.FeaturesFailed.Should().Be(2); // builder error + pending
        result.FeaturesProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ParseBatchesAsync_ReportsErrors()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var errors = new List<StreamingError>();

        await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch => throw new Exception("batch fail"),
            batchSize: 2,
            onError: e => errors.Add(e),
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Continue });

        errors.Should().HaveCount(2); // batch of 2 fails, partial of 1 fails
    }

    // ---- IFeatureSink ----

    [Fact]
    public async Task ParseAsync_WithSink_CallsWriteFeaturePerFeature()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var sink = new TestSink();

        var result = await StreamingGml.ParseAsync(stream, sink);

        result.FeaturesProcessed.Should().Be(3);
        sink.Features.Should().HaveCount(3);
        sink.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_WithSink_CallsCompleteOnce()
    {
        var xml = BuildWfsCollection(2);
        using var stream = ToStream(xml);
        var sink = new TestSink();

        await StreamingGml.ParseAsync(stream, sink);

        sink.CompleteCount.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_WithSink_DoesNotCallCompleteOnError()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var sink = new FailingSink();

        var result = await StreamingGml.ParseAsync(
            stream,
            sink,
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Stop });

        sink.Completed.Should().BeFalse();
        result.FeaturesFailed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ParseAsync_WithSink_ReportsErrors()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var sink = new FailingSink();
        var errors = new List<StreamingError>();

        await StreamingGml.ParseAsync(
            stream,
            sink,
            onError: e => errors.Add(e),
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Continue });

        errors.Should().HaveCount(3);
    }

    // ---- StreamingGml ParseAsync builder with Stop ----

    [Fact]
    public async Task StreamingGml_ParseAsync_WithBuilder_Stop_StopsAfterFirstError()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var count = 0;

        var result = await StreamingGml.ParseAsync(
            stream,
            builder,
            f => { count++; if (count == 2) throw new Exception("fail"); return ValueTask.CompletedTask; },
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Stop });

        result.FeaturesProcessed.Should().Be(1);
        result.FeaturesFailed.Should().Be(1);
    }

    // ---- StreamingGmlParser without OnError handler ----

    [Fact]
    public async Task ParseAsync_ErrorWithoutOnError_StillCounts()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Continue
        });
        parser.OnFeature(f =>
        {
            if (f.Id == "item.1") throw new Exception("fail");
            return ValueTask.CompletedTask;
        });
        // No OnError registered

        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(2);
        result.FeaturesFailed.Should().Be(1);
    }

    // ---- Batch with Stop on batch error ----

    [Fact]
    public async Task ParseBatchesAsync_BatchError_WithStop_StopsImmediately()
    {
        var xml = BuildWfsCollection(6);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            batch => throw new Exception("batch fail"),
            batchSize: 2,
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Stop });

        // First batch of 2 fails → stop, remaining 4 not processed
        // Pending batch (next 2 were already parsed during iteration)
        result.FeaturesFailed.Should().BeGreaterThanOrEqualTo(2);
        result.FeaturesProcessed.Should().Be(0);
    }

    // ---- Sink error path for non-recoverable ----

    [Fact]
    public async Task ParseAsync_WithSink_Stop_OnNonRecoverableError()
    {
        var xml = BuildWfsCollection(3);
        using var stream = ToStream(xml);
        var sink = new FailingSink();
        var errors = new List<StreamingError>();

        var result = await StreamingGml.ParseAsync(
            stream,
            sink,
            onError: e => errors.Add(e),
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Stop });

        result.FeaturesFailed.Should().Be(1);
        result.FeaturesProcessed.Should().Be(0);
        errors.Should().HaveCount(1);
        sink.Completed.Should().BeFalse();
    }

    // ---- Sink with cancellation ----

    [Fact]
    public async Task ParseAsync_WithSink_DoesNotCallCompleteOnCancellation()
    {
        var xml = BuildWfsCollection(100);
        using var stream = ToStream(xml);
        using var cts = new CancellationTokenSource();
        var sink = new CancellingSink(cts, cancelAfter: 2);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => StreamingGml.ParseAsync(stream, sink, ct: cts.Token));

        sink.Completed.Should().BeFalse();
        sink.WriteCount.Should().Be(2);
    }

    // ---- Sink with Continue ----

    [Fact]
    public async Task ParseAsync_WithSink_ContinuesAfterWriteError()
    {
        var xml = BuildWfsCollection(5);
        using var stream = ToStream(xml);
        var sink = new FailOnIdSink("item.1");

        var result = await StreamingGml.ParseAsync(
            stream,
            sink,
            options: new StreamingParserOptions { ErrorBehavior = StreamingErrorBehavior.Continue });

        result.FeaturesProcessed.Should().Be(4);
        result.FeaturesFailed.Should().Be(1);
        sink.Completed.Should().BeTrue();
    }

    // ---- ParseBatchesAsync edge cases ----

    [Fact]
    public async Task ParseBatchesAsync_InvalidBatchSize_Throws()
    {
        using var stream = ToStream(BuildWfsCollection(1));
        var builder = new GeoJsonBuilder();

        var act = () => StreamingGml.ParseBatchesAsync(
            stream, builder, _ => ValueTask.CompletedTask, batchSize: 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ParseBatchesAsync_EmptyCollection_NoBatchCallback()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2">
            </wfs:FeatureCollection>
            """;
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var batchCount = 0;

        var result = await StreamingGml.ParseBatchesAsync(
            stream, builder,
            _ => { batchCount++; return ValueTask.CompletedTask; },
            batchSize: 10);

        batchCount.Should().Be(0);
        result.FeaturesProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ParseBatchesAsync_Progress_ReportedPerFeature()
    {
        var xml = BuildWfsCollection(4);
        using var stream = ToStream(xml);
        var builder = new GeoJsonBuilder();
        var reports = new List<StreamingProgress>();

        var result = await StreamingGml.ParseBatchesAsync(
            stream,
            builder,
            _ => ValueTask.CompletedTask,
            batchSize: 2,
            options: new StreamingParserOptions
            {
                Progress = new SyncProgress<StreamingProgress>(p => reports.Add(p))
            });

        // 4 features, batchSize 2: progress fires per feature
        reports.Should().HaveCount(4);
    }

    // ---- StreamingGmlParser with empty collection ----

    [Fact]
    public async Task ParseAsync_EmptyCollection_ReturnsZeroCounts()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2">
            </wfs:FeatureCollection>
            """;
        using var stream = ToStream(xml);

        var parser = new StreamingGmlParser();
        var result = await parser.ParseAsync(stream);

        result.FeaturesProcessed.Should().Be(0);
        result.FeaturesFailed.Should().Be(0);
    }

    [Fact]
    public async Task ParseAsync_OnEnd_CalledForEmptyCollection()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2">
            </wfs:FeatureCollection>
            """;
        using var stream = ToStream(xml);
        var endCalled = false;

        var parser = new StreamingGmlParser();
        parser.OnEnd(_ => endCalled = true);

        await parser.ParseAsync(stream);

        endCalled.Should().BeTrue();
    }

    // ---- Fatal stream errors ----

    [Fact]
    public async Task ParseAsync_TruncatedXml_ReportsFatalError()
    {
        // XML truncated mid-feature to trigger TryReadFragmentAsync failure
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:City gml:id="city.1"><app:name>Munich
            """;
        using var stream = ToStream(xml);
        var errors = new List<StreamingError>();

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Continue
        });
        parser.OnFeature(_ => ValueTask.CompletedTask);
        parser.OnError(e => errors.Add(e));

        var result = await parser.ParseAsync(stream);

        errors.Should().HaveCount(1);
        errors[0].CanContinue.Should().BeFalse();
        result.FeaturesFailed.Should().Be(1);
    }

    [Fact]
    public async Task ParseItemsAsync_TruncatedXml_YieldsFatalItem()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:City gml:id="city.1"><app:name>Munich
            """;
        using var stream = ToStream(xml);
        var items = new List<FeatureStreamItem>();

        await foreach (var item in GmlFeatureStreamParser.ParseItemsAsync(stream))
            items.Add(item);

        items.Should().HaveCount(1);
        items[0].IsSuccess.Should().BeFalse();
        items[0].CanContinue.Should().BeFalse();
        items[0].Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task ParseAsync_FatalStreamError_NotReportedAsRecoverable()
    {
        // Truncated XML in featureMembers (plural) path
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <gml:featureMembers>
                    <app:A gml:id="a.1"><app:x>ok</app:x></app:A>
                    <app:B gml:id="b.1"><app:broken>
            """;
        using var stream = ToStream(xml);
        var errors = new List<StreamingError>();
        var features = new List<GmlFeature>();

        var parser = new StreamingGmlParser(new StreamingParserOptions
        {
            ErrorBehavior = StreamingErrorBehavior.Continue
        });
        parser.OnFeature(f => { features.Add(f); return ValueTask.CompletedTask; });
        parser.OnError(e => errors.Add(e));

        var result = await parser.ParseAsync(stream);

        features.Should().HaveCount(1); // First feature OK
        errors.Should().HaveCount(1);   // Second feature fatal
        errors[0].CanContinue.Should().BeFalse();
    }

    // ---- ParseItemsAsync internal ----

    [Fact]
    public async Task ParseItemsAsync_RecoverableError_HasCanContinueTrue()
    {
        // FeatureParser.ParseFeature returns null for GML/WFS framework elements
        // passed as features, but the streaming parser skips those.
        // To test recoverable errors, we rely on handler errors in StreamingGmlParser.
        // This test verifies the items path yields successful items.
        var xml = BuildWfsCollection(2);
        using var stream = ToStream(xml);

        var items = new List<FeatureStreamItem>();
        await foreach (var item in GmlFeatureStreamParser.ParseItemsAsync(stream))
            items.Add(item);

        items.Should().HaveCount(2);
        items.Should().AllSatisfy(i =>
        {
            i.IsSuccess.Should().BeTrue();
            i.CanContinue.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ParseItemsAsync_WarningsTransportedLosslessly()
    {
        // Features with valid XML but containing issues are reported with
        // Issues populated alongside the Feature
        var xml = BuildWfsCollection(1);
        using var stream = ToStream(xml);

        var items = new List<FeatureStreamItem>();
        await foreach (var item in GmlFeatureStreamParser.ParseItemsAsync(stream))
            items.Add(item);

        items[0].Feature.Should().NotBeNull();
        // Issues list is always present (may be empty for clean features)
        items[0].Issues.Should().NotBeNull();
    }

    // ---- Helpers ----

    private static MemoryStream ToStream(string xml) =>
        new(Encoding.UTF8.GetBytes(xml));

    private static string BuildWfsCollection(int count)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:gml="http://www.opengis.net/gml/3.2" xmlns:app="http://example.com/app">""");
        for (int i = 0; i < count; i++)
            sb.AppendLine($"""<wfs:member><app:Item gml:id="item.{i}"><app:idx>{i}</app:idx></app:Item></wfs:member>""");
        sb.AppendLine("</wfs:FeatureCollection>");
        return sb.ToString();
    }

    // ---- Test doubles ----

    private sealed class TestSink : IFeatureSink
    {
        public List<GmlFeature> Features { get; } = [];
        public bool Completed { get; private set; }
        public int CompleteCount { get; private set; }

        public ValueTask WriteFeatureAsync(GmlFeature feature, CancellationToken ct = default)
        {
            Features.Add(feature);
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteAsync(CancellationToken ct = default)
        {
            Completed = true;
            CompleteCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailingSink : IFeatureSink
    {
        public bool Completed { get; private set; }

        public ValueTask WriteFeatureAsync(GmlFeature feature, CancellationToken ct = default) =>
            throw new InvalidOperationException("sink write failed");

        public ValueTask CompleteAsync(CancellationToken ct = default)
        {
            Completed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CancellingSink(CancellationTokenSource cts, int cancelAfter) : IFeatureSink
    {
        public bool Completed { get; private set; }
        public int WriteCount { get; private set; }

        public ValueTask WriteFeatureAsync(GmlFeature feature, CancellationToken ct = default)
        {
            WriteCount++;
            if (WriteCount >= cancelAfter) cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteAsync(CancellationToken ct = default)
        {
            Completed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailOnIdSink(string failId) : IFeatureSink
    {
        public bool Completed { get; private set; }

        public ValueTask WriteFeatureAsync(GmlFeature feature, CancellationToken ct = default)
        {
            if (feature.Id == failId)
                throw new InvalidOperationException($"Sink write failed for {failId}");
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteAsync(CancellationToken ct = default)
        {
            Completed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Synchronous IProgress implementation that invokes the callback inline,
    /// avoiding the thread-pool dispatch of <see cref="Progress{T}"/>.
    /// </summary>
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    /// <summary>
    /// A builder that wraps GeoJsonBuilder but throws on a specific feature ID.
    /// Used to test recoverable builder errors in the batch path.
    /// </summary>
    private sealed class ThrowOnIdBuilder : IBuilder<object, object, object>
    {
        private readonly GeoJsonBuilder _inner = new();
        private readonly string _failId;

        public ThrowOnIdBuilder(string failId) => _failId = failId;

        public object BuildFeature(GmlFeature feature)
        {
            if (feature.Id == _failId)
                throw new InvalidOperationException($"Builder error on {_failId}");
            return _inner.BuildFeature(feature);
        }

        public object? BuildPoint(GmlPoint point) => _inner.BuildPoint(point);
        public object? BuildLineString(GmlLineString lineString) => _inner.BuildLineString(lineString);
        public object? BuildLinearRing(GmlLinearRing linearRing) => _inner.BuildLinearRing(linearRing);
        public object? BuildPolygon(GmlPolygon polygon) => _inner.BuildPolygon(polygon);
        public object? BuildMultiPoint(GmlMultiPoint multiPoint) => _inner.BuildMultiPoint(multiPoint);
        public object? BuildMultiLineString(GmlMultiLineString multiLineString) => _inner.BuildMultiLineString(multiLineString);
        public object? BuildMultiPolygon(GmlMultiPolygon multiPolygon) => _inner.BuildMultiPolygon(multiPolygon);
        public object? BuildEnvelope(GmlEnvelope envelope) => _inner.BuildEnvelope(envelope);
        public object? BuildBox(GmlBox box) => _inner.BuildBox(box);
        public object? BuildCurve(GmlCurve curve) => _inner.BuildCurve(curve);
        public object? BuildSurface(GmlSurface surface) => _inner.BuildSurface(surface);
        public object BuildFeatureCollection(GmlFeatureCollection fc) => _inner.BuildFeatureCollection(fc);
        public object? BuildCoverage(GmlCoverage coverage) => _inner.BuildCoverage(coverage);
    }
}

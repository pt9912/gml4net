using System.Text;
using FluentAssertions;
using Gml4Net.Model.Feature;
using Gml4Net.Parser;
using Gml4Net.Parser.Streaming;
using Xunit;

namespace Gml4Net.Tests.Streaming;

public class StreamParserTests
{
    [Fact]
    public async Task ParseAsync_WithWfs20Members_StreamsFeatures()
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

        var features = await CollectAsync(GmlFeatureStreamParser.ParseAsync(stream));

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

        var features = await CollectAsync(GmlFeatureStreamParser.ParseAsync(stream));

        features.Should().HaveCount(1);
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

        var features = await CollectAsync(GmlFeatureStreamParser.ParseAsync(stream));

        features.Should().HaveCount(2);
        features[0].Id.Should().Be("a.1");
        features[1].Id.Should().Be("b.1");
    }

    [Fact]
    public async Task ParseAsync_MatchesDomResult()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Sensor gml:id="s.1">
                        <app:value>42.5</app:value>
                        <app:location>
                            <gml:Point><gml:pos>10 20</gml:pos></gml:Point>
                        </app:location>
                    </app:Sensor>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        // DOM parse
        var domResult = GmlParser.ParseXmlString(xml);
        var domFeatures = (domResult.Document!.Root as GmlFeatureCollection)!.Features;

        // Streaming parse
        using var stream = ToStream(xml);
        var streamFeatures = await CollectAsync(GmlFeatureStreamParser.ParseAsync(stream));

        streamFeatures.Should().HaveCount(domFeatures.Count);
        streamFeatures[0].Id.Should().Be(domFeatures[0].Id);
        streamFeatures[0].Properties.ContainsKey("value").Should().Be(domFeatures[0].Properties.ContainsKey("value"));
    }

    [Fact]
    public async Task ParseAsync_LargeDocument_StreamsAllFeatures()
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:gml="http://www.opengis.net/gml/3.2" xmlns:app="http://example.com/app">""");
        for (int i = 0; i < 10_000; i++)
        {
            sb.AppendLine($"""<wfs:member><app:Item gml:id="item.{i}"><app:idx>{i}</app:idx></app:Item></wfs:member>""");
        }
        sb.AppendLine("</wfs:FeatureCollection>");

        using var stream = ToStream(sb.ToString());
        var features = await CollectAsync(GmlFeatureStreamParser.ParseAsync(stream));

        features.Should().HaveCount(10_000);
        features[0].Id.Should().Be("item.0");
        features[9999].Id.Should().Be("item.9999");
    }

    [Fact]
    public async Task ParseAsync_WithCancellation_StopsEarly()
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:gml="http://www.opengis.net/gml/3.2" xmlns:app="http://example.com/app">""");
        for (int i = 0; i < 1000; i++)
            sb.AppendLine($"""<wfs:member><app:Item gml:id="i.{i}"><app:x>{i}</app:x></app:Item></wfs:member>""");
        sb.AppendLine("</wfs:FeatureCollection>");

        using var cts = new CancellationTokenSource();
        using var stream = ToStream(sb.ToString());
        var count = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var feature in GmlFeatureStreamParser.ParseAsync(stream, cts.Token))
            {
                count++;
                if (count == 5) cts.Cancel();
            }
        });

        count.Should().Be(5);
    }

    [Fact]
    public async Task ProcessFeaturesAsync_ReturnsCount()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member><app:A gml:id="a.1"><app:x>1</app:x></app:A></wfs:member>
                <wfs:member><app:B gml:id="b.1"><app:x>2</app:x></app:B></wfs:member>
                <wfs:member><app:C gml:id="c.1"><app:x>3</app:x></app:C></wfs:member>
            </wfs:FeatureCollection>
            """;
        using var stream = ToStream(xml);
        var collected = new List<GmlFeature>();

        var count = await GmlFeatureStreamParser.ProcessFeaturesAsync(stream, f =>
        {
            collected.Add(f);
            return Task.CompletedTask;
        });

        count.Should().Be(3);
        collected.Should().HaveCount(3);
    }

    [Fact]
    public async Task ParseAsync_EmptyCollection_YieldsNothing()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2">
            </wfs:FeatureCollection>
            """;
        using var stream = ToStream(xml);

        var features = await CollectAsync(GmlFeatureStreamParser.ParseAsync(stream));

        features.Should().BeEmpty();
    }

    // ---- Helpers ----

    private static MemoryStream ToStream(string xml) =>
        new(Encoding.UTF8.GetBytes(xml));

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }
}

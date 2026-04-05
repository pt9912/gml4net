using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

public class GmlParserBuilderTests
{
    private const string PointGml = """
        <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
            <gml:pos>8.5 47.3</gml:pos>
        </gml:Point>
        """;

    private const string FeatureGml = """
        <app:Building gml:id="b.1"
            xmlns:app="http://example.com/app"
            xmlns:gml="http://www.opengis.net/gml/3.2">
            <app:name>Tower</app:name>
            <app:geom>
                <gml:Point><gml:pos>8.5 47.3</gml:pos></gml:Point>
            </app:geom>
        </app:Building>
        """;

    private const string FeatureCollectionGml = """
        <wfs:FeatureCollection
            xmlns:wfs="http://www.opengis.net/wfs/2.0"
            xmlns:app="http://example.com/app"
            xmlns:gml="http://www.opengis.net/gml/3.2">
            <wfs:member>
                <app:Building gml:id="b.1">
                    <app:name>Tower</app:name>
                    <app:geom>
                        <gml:Point><gml:pos>8.5 47.3</gml:pos></gml:Point>
                    </app:geom>
                </app:Building>
            </wfs:member>
        </wfs:FeatureCollection>
        """;

    // ---- Factory ----

    [Fact]
    public void Create_ReturnsGenericParser()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        parser.Should().NotBeNull();
        parser.Should().BeOfType<GmlParser<JsonObject, JsonObject, JsonObject>>();
    }

    [Fact]
    public void Create_WithNullBuilder_ThrowsArgumentNullException()
    {
        var act = () => new GmlParser<JsonObject, JsonObject, JsonObject>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Parse(string) ----

    [Fact]
    public void Parse_Point_ReturnsGeometry()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        var result = parser.Parse(PointGml);

        result.HasErrors.Should().BeFalse();
        result.Geometry.Should().NotBeNull();
        result.Geometry!["type"]!.GetValue<string>().Should().Be("Point");
        result.Feature.Should().BeNull();
        result.Collection.Should().BeNull();
        result.Coverage.Should().BeNull();
        result.Document.Should().NotBeNull();
    }

    [Fact]
    public void Parse_Feature_ReturnsFeature()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        var result = parser.Parse(FeatureGml);

        result.HasErrors.Should().BeFalse();
        result.Feature.Should().NotBeNull();
        result.Feature!["type"]!.GetValue<string>().Should().Be("Feature");
        result.Geometry.Should().BeNull();
        result.Collection.Should().BeNull();
    }

    [Fact]
    public void Parse_FeatureCollection_ReturnsCollection()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        var result = parser.Parse(FeatureCollectionGml);

        result.HasErrors.Should().BeFalse();
        result.Collection.Should().NotBeNull();
        result.Collection!["type"]!.GetValue<string>().Should().Be("FeatureCollection");
        result.Geometry.Should().BeNull();
        result.Feature.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidXml_ReturnsErrors()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        var result = parser.Parse("<broken");

        result.HasErrors.Should().BeTrue();
        result.Document.Should().BeNull();
        result.Geometry.Should().BeNull();
        result.Feature.Should().BeNull();
        result.Collection.Should().BeNull();
    }

    // ---- Multiple builders ----

    [Fact]
    public void Parse_WithWktBuilder_ReturnsWkt()
    {
        var parser = GmlParser.Create(WktBuilder.Instance);

        var result = parser.Parse(PointGml);

        result.Geometry.Should().Be("POINT (8.5 47.3)");
    }

    [Fact]
    public void Parse_WithKmlBuilder_ReturnsKml()
    {
        var parser = GmlParser.Create(KmlBuilder.Instance);

        var result = parser.Parse(PointGml);

        result.Geometry.Should().NotBeNull();
        result.Geometry!.Name.LocalName.Should().Be("Point");
    }

    // ---- Parse(Stream) ----

    [Fact]
    public void Parse_Stream_ReturnsGeometry()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(PointGml));

        var result = parser.Parse(stream);

        result.HasErrors.Should().BeFalse();
        result.Geometry.Should().NotBeNull();
    }

    // ---- Parse(ReadOnlySpan<byte>) ----

    [Fact]
    public void Parse_Bytes_ReturnsGeometry()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);
        var bytes = Encoding.UTF8.GetBytes(PointGml);

        var result = parser.Parse(bytes);

        result.HasErrors.Should().BeFalse();
        result.Geometry.Should().NotBeNull();
    }

    // ---- Document metadata ----

    [Fact]
    public void Parse_PreservesDocumentMetadata()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        var result = parser.Parse(PointGml);

        result.Document.Should().NotBeNull();
        result.Document!.Version.Should().Be(GmlVersion.V3_2);
    }

    [Fact]
    public void Parse_PreservesIssues()
    {
        var parser = GmlParser.Create(GeoJsonBuilder.Instance);

        var result = parser.Parse(FeatureCollectionGml);

        result.Issues.Should().NotBeNull();
    }

    // ---- Reuse ----

    [Fact]
    public void Parser_CanBeReused()
    {
        var parser = GmlParser.Create(WktBuilder.Instance);

        var r1 = parser.Parse(PointGml);
        var r2 = parser.Parse(PointGml);

        r1.Geometry.Should().Be(r2.Geometry);
    }
}

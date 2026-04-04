using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

public class EdgeCaseTests
{
    [Fact]
    public void ParseXmlString_WithMalformedXml_ReturnsError()
    {
        var xml = "<not valid xml<>";

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Document.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "invalid_xml");
    }

    [Fact]
    public void ParseXmlString_WithEmptyRoot_ReturnsError()
    {
        var xml = "<root/>";

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Document.Should().BeNull();
        result.Issues.Should().Contain(i => i.Code == "unknown_root");
    }

    [Fact]
    public void ParseXmlString_WithPointMissingPos_ReturnsError()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_coordinates");
    }

    [Fact]
    public void ParseXmlString_WithPolygonMissingExterior_ReturnsError()
    {
        var xml = """
            <gml:Polygon xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:Polygon>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_exterior");
    }

    [Fact]
    public void ParseXmlString_WithUnknownGmlElement_ReturnsWarning()
    {
        var xml = """
            <gml:CompositeCurve xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:CompositeCurve>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "unknown_root");
    }

    [Fact]
    public void ParseXmlString_WithLineStringMissingCoords_ReturnsError()
    {
        var xml = """
            <gml:LineString xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:LineString>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_coordinates");
    }

    [Fact]
    public void ParseXmlString_WithEnvelopeMissingCorners_ReturnsError()
    {
        var xml = """
            <gml:Envelope xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:Envelope>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_corners");
    }

    [Fact]
    public void ParseStream_WithValidGml_ReturnsResult()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>1.0 2.0</gml:pos>
            </gml:Point>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var result = GmlParser.ParseStream(stream);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlPoint>();
    }

    [Fact]
    public void ParseBytes_WithValidGml_ReturnsResult()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>5.0 6.0</gml:pos>
            </gml:Point>
            """;
        var bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        var result = GmlParser.ParseBytes(bytes);

        result.HasErrors.Should().BeFalse();
        var point = result.Document!.Root.Should().BeOfType<GmlPoint>().Subject;
        point.Coordinate.Should().Be(new GmlCoordinate(5.0, 6.0));
    }

    [Fact]
    public void ParseXmlString_WithMultiplePos_ReturnsLineString()
    {
        var xml = """
            <gml:LineString xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>0 0</gml:pos>
                <gml:pos>1 1</gml:pos>
                <gml:pos>2 2</gml:pos>
            </gml:LineString>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var ls = result.Document!.Root.Should().BeOfType<GmlLineString>().Subject;
        ls.Coordinates.Should().HaveCount(3);
    }
}

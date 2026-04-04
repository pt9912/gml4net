using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

/// <summary>
/// Tests for coordinate parsing edge cases, invalid input, and TryParse error paths.
/// </summary>
public class CoordinateParsingTests
{
    // ---- ParsePos error paths ----

    [Fact]
    public void ParsePos_WithInvalidNumber_ReturnsZeroAndIssue()
    {
        var issues = new List<GmlParseIssue>();
        var coord = XmlHelpers.ParsePos("abc def", null, issues);

        coord.Should().Be(new GmlCoordinate(0, 0));
        issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }

    [Fact]
    public void ParsePos_WithSingleValue_ReturnsZeroCoordinate()
    {
        var coord = XmlHelpers.ParsePos("10.0");
        coord.Should().Be(new GmlCoordinate(0, 0));
    }

    [Fact]
    public void ParsePos_WithEmptyString_ReturnsZeroCoordinate()
    {
        var coord = XmlHelpers.ParsePos("   ");
        coord.Should().Be(new GmlCoordinate(0, 0));
    }

    [Fact]
    public void ParsePos_With4D_ReturnsFourComponents()
    {
        var coord = XmlHelpers.ParsePos("1.0 2.0 3.0 4.0");
        coord.Should().Be(new GmlCoordinate(1.0, 2.0, 3.0, 4.0));
        coord.Dimension.Should().Be(4);
    }

    [Fact]
    public void ParsePos_WithSrsDimension2_DropsZ()
    {
        var coord = XmlHelpers.ParsePos("1.0 2.0 3.0", srsDimension: 2);
        coord.Should().Be(new GmlCoordinate(1.0, 2.0));
        coord.Dimension.Should().Be(2);
    }

    // ---- ParsePosList error paths ----

    [Fact]
    public void ParsePosList_WithInvalidNumber_ReturnsEmptyAndIssue()
    {
        var issues = new List<GmlParseIssue>();
        var coords = XmlHelpers.ParsePosList("1.0 abc 3.0 4.0", 2, issues);

        coords.Should().BeEmpty();
        issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }

    [Fact]
    public void ParsePosList_WithEmptyString_ReturnsEmpty()
    {
        var coords = XmlHelpers.ParsePosList("", 2);
        coords.Should().BeEmpty();
    }

    [Fact]
    public void ParsePosList_WithWhitespaceOnly_ReturnsEmpty()
    {
        var coords = XmlHelpers.ParsePosList("   \t\n  ", 2);
        coords.Should().BeEmpty();
    }

    [Fact]
    public void ParsePosList_WithDimension1_UsesDimension2()
    {
        var coords = XmlHelpers.ParsePosList("1.0 2.0 3.0 4.0", 1);
        coords.Should().HaveCount(2);
        coords[0].Should().Be(new GmlCoordinate(1.0, 2.0));
    }

    [Fact]
    public void ParsePosList_With3D_Returns3DCoordinates()
    {
        var coords = XmlHelpers.ParsePosList("1.0 2.0 3.0 4.0 5.0 6.0", 3);
        coords.Should().HaveCount(2);
        coords[0].Should().Be(new GmlCoordinate(1.0, 2.0, 3.0));
        coords[1].Should().Be(new GmlCoordinate(4.0, 5.0, 6.0));
    }

    [Fact]
    public void ParsePosList_With4D_Returns4DCoordinates()
    {
        var coords = XmlHelpers.ParsePosList("1 2 3 4 5 6 7 8", 4);
        coords.Should().HaveCount(2);
        coords[0].Dimension.Should().Be(4);
    }

    // ---- ParseGml2Coordinates error paths ----

    [Fact]
    public void ParseGml2Coordinates_WithInvalidNumber_SkipsAndAddsIssue()
    {
        var issues = new List<GmlParseIssue>();
        var coords = XmlHelpers.ParseGml2Coordinates("abc,def 3.0,4.0", issues);

        coords.Should().HaveCount(1);
        coords[0].Should().Be(new GmlCoordinate(3.0, 4.0));
        issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }

    [Fact]
    public void ParseGml2Coordinates_WithEmptyString_ReturnsEmpty()
    {
        var coords = XmlHelpers.ParseGml2Coordinates("");
        coords.Should().BeEmpty();
    }

    [Fact]
    public void ParseGml2Coordinates_WithSingleValue_SkipsTuple()
    {
        var coords = XmlHelpers.ParseGml2Coordinates("10.0");
        coords.Should().BeEmpty();
    }

    [Fact]
    public void ParseGml2Coordinates_With3D_Returns3DCoordinates()
    {
        var coords = XmlHelpers.ParseGml2Coordinates("1.0,2.0,3.0");
        coords.Should().HaveCount(1);
        coords[0].Should().Be(new GmlCoordinate(1.0, 2.0, 3.0));
    }

    // ---- GML document with invalid coordinates ----

    [Fact]
    public void ParseXmlString_WithInvalidPosContent_ReturnsIssueNotException()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>not_a_number also_bad</gml:pos>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.Issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }

    [Fact]
    public void ParseXmlString_WithInvalidPosListContent_ReturnsIssueNotException()
    {
        var xml = """
            <gml:LineString xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:posList srsDimension="2">1.0 bad 3.0 4.0</gml:posList>
            </gml:LineString>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.Issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }

    [Fact]
    public void ParseXmlString_WithInvalidGml2Coords_ReturnsIssueNotException()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>bad,data</gml:coordinates>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.Issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }

    // ---- ParseCoordElement paths ----

    [Fact]
    public void ParseXmlString_WithGml2CoordElement_ReturnsPoint()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:coord>
                    <gml:X>10.0</gml:X>
                    <gml:Y>20.0</gml:Y>
                </gml:coord>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var pt = result.Document!.Root.Should().BeOfType<GmlPoint>().Subject;
        pt.Coordinate.Should().Be(new GmlCoordinate(10.0, 20.0));
    }

    [Fact]
    public void ParseXmlString_WithGml2CoordElementAndZ_Returns3DPoint()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:coord>
                    <gml:X>1</gml:X>
                    <gml:Y>2</gml:Y>
                    <gml:Z>3</gml:Z>
                </gml:coord>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var pt = result.Document!.Root.Should().BeOfType<GmlPoint>().Subject;
        pt.Coordinate.Should().Be(new GmlCoordinate(1, 2, 3));
    }

    [Fact]
    public void ParseXmlString_WithInvalidCoordElement_ReturnsIssue()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:coord>
                    <gml:X>bad</gml:X>
                    <gml:Y>data</gml:Y>
                </gml:coord>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.Issues.Should().Contain(i => i.Code == "invalid_coordinate");
    }
}

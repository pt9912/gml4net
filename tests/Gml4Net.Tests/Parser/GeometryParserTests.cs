using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

public class GeometryParserTests
{
    // ---- Point ----

    [Fact]
    public void ParseXmlString_WithGml32Point_ReturnsGmlPoint()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>10.0 20.0</gml:pos>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document.Should().NotBeNull();
        result.Document!.Version.Should().Be(GmlVersion.V3_2);
        var point = result.Document.Root.Should().BeOfType<GmlPoint>().Subject;
        point.Coordinate.Should().Be(new GmlCoordinate(10.0, 20.0));
    }

    [Fact]
    public void ParseXmlString_WithGml2Point_ReturnsGmlPoint()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>10.0,20.0</gml:coordinates>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var point = result.Document!.Root.Should().BeOfType<GmlPoint>().Subject;
        point.Coordinate.Should().Be(new GmlCoordinate(10.0, 20.0));
    }

    [Fact]
    public void ParseXmlString_WithGml32Point3D_ReturnsGmlPointWith3D()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2" srsDimension="3">
                <gml:pos>10.0 20.0 30.0</gml:pos>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var point = result.Document!.Root.Should().BeOfType<GmlPoint>().Subject;
        point.Coordinate.Should().Be(new GmlCoordinate(10.0, 20.0, 30.0));
        point.Coordinate.Dimension.Should().Be(3);
    }

    [Fact]
    public void ParseXmlString_WithSrsName_PreservesSrsName()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2" srsName="EPSG:4326">
                <gml:pos>10.0 20.0</gml:pos>
            </gml:Point>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var point = result.Document!.Root.Should().BeOfType<GmlPoint>().Subject;
        point.SrsName.Should().Be("EPSG:4326");
    }

    // ---- LineString ----

    [Fact]
    public void ParseXmlString_WithGml32LineString_ReturnsGmlLineString()
    {
        var xml = """
            <gml:LineString xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:posList srsDimension="2">0.0 0.0 10.0 0.0 10.0 10.0</gml:posList>
            </gml:LineString>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var ls = result.Document!.Root.Should().BeOfType<GmlLineString>().Subject;
        ls.Coordinates.Should().HaveCount(3);
        ls.Coordinates[0].Should().Be(new GmlCoordinate(0.0, 0.0));
        ls.Coordinates[2].Should().Be(new GmlCoordinate(10.0, 10.0));
    }

    [Fact]
    public void ParseXmlString_WithGml2LineString_ReturnsGmlLineString()
    {
        var xml = """
            <gml:LineString xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>0.0,0.0 10.0,0.0 10.0,10.0</gml:coordinates>
            </gml:LineString>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var ls = result.Document!.Root.Should().BeOfType<GmlLineString>().Subject;
        ls.Coordinates.Should().HaveCount(3);
    }

    // ---- Polygon ----

    [Fact]
    public void ParseXmlString_WithGml32Polygon_ReturnsGmlPolygon()
    {
        var xml = """
            <gml:Polygon xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:exterior>
                    <gml:LinearRing>
                        <gml:posList srsDimension="2">0 0 10 0 10 10 0 10 0 0</gml:posList>
                    </gml:LinearRing>
                </gml:exterior>
            </gml:Polygon>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var poly = result.Document!.Root.Should().BeOfType<GmlPolygon>().Subject;
        poly.Exterior.Coordinates.Should().HaveCount(5);
        poly.Interior.Should().BeEmpty();
    }

    [Fact]
    public void ParseXmlString_WithGml2Polygon_ReturnsGmlPolygon()
    {
        var xml = """
            <gml:Polygon xmlns:gml="http://www.opengis.net/gml">
                <gml:outerBoundaryIs>
                    <gml:LinearRing>
                        <gml:coordinates>0,0 10,0 10,10 0,10 0,0</gml:coordinates>
                    </gml:LinearRing>
                </gml:outerBoundaryIs>
            </gml:Polygon>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var poly = result.Document!.Root.Should().BeOfType<GmlPolygon>().Subject;
        poly.Exterior.Coordinates.Should().HaveCount(5);
    }

    [Fact]
    public void ParseXmlString_WithPolygonAndHole_ReturnsPolygonWithInterior()
    {
        var xml = """
            <gml:Polygon xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:exterior>
                    <gml:LinearRing>
                        <gml:posList srsDimension="2">0 0 100 0 100 100 0 100 0 0</gml:posList>
                    </gml:LinearRing>
                </gml:exterior>
                <gml:interior>
                    <gml:LinearRing>
                        <gml:posList srsDimension="2">10 10 20 10 20 20 10 20 10 10</gml:posList>
                    </gml:LinearRing>
                </gml:interior>
            </gml:Polygon>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var poly = result.Document!.Root.Should().BeOfType<GmlPolygon>().Subject;
        poly.Interior.Should().HaveCount(1);
        poly.Interior[0].Coordinates.Should().HaveCount(5);
    }

    // ---- Envelope / Box ----

    [Fact]
    public void ParseXmlString_WithEnvelope_ReturnsGmlEnvelope()
    {
        var xml = """
            <gml:Envelope xmlns:gml="http://www.opengis.net/gml/3.2" srsName="EPSG:4326">
                <gml:lowerCorner>0.0 0.0</gml:lowerCorner>
                <gml:upperCorner>10.0 10.0</gml:upperCorner>
            </gml:Envelope>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var env = result.Document!.Root.Should().BeOfType<GmlEnvelope>().Subject;
        env.LowerCorner.Should().Be(new GmlCoordinate(0.0, 0.0));
        env.UpperCorner.Should().Be(new GmlCoordinate(10.0, 10.0));
        env.SrsName.Should().Be("EPSG:4326");
    }

    [Fact]
    public void ParseXmlString_WithGml2Box_ReturnsGmlBox()
    {
        var xml = """
            <gml:Box xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>0.0,0.0 10.0,10.0</gml:coordinates>
            </gml:Box>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var box = result.Document!.Root.Should().BeOfType<GmlBox>().Subject;
        box.LowerCorner.Should().Be(new GmlCoordinate(0.0, 0.0));
        box.UpperCorner.Should().Be(new GmlCoordinate(10.0, 10.0));
    }

    // ---- MultiPoint ----

    [Fact]
    public void ParseXmlString_WithMultiPoint_ReturnsGmlMultiPoint()
    {
        var xml = """
            <gml:MultiPoint xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pointMember>
                    <gml:Point><gml:pos>1.0 2.0</gml:pos></gml:Point>
                </gml:pointMember>
                <gml:pointMember>
                    <gml:Point><gml:pos>3.0 4.0</gml:pos></gml:Point>
                </gml:pointMember>
            </gml:MultiPoint>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mp = result.Document!.Root.Should().BeOfType<GmlMultiPoint>().Subject;
        mp.Points.Should().HaveCount(2);
        mp.Points[0].Coordinate.Should().Be(new GmlCoordinate(1.0, 2.0));
        mp.Points[1].Coordinate.Should().Be(new GmlCoordinate(3.0, 4.0));
    }

    // ---- MultiLineString ----

    [Fact]
    public void ParseXmlString_WithMultiLineString_ReturnsGmlMultiLineString()
    {
        var xml = """
            <gml:MultiLineString xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:lineStringMember>
                    <gml:LineString>
                        <gml:posList srsDimension="2">0 0 1 1</gml:posList>
                    </gml:LineString>
                </gml:lineStringMember>
                <gml:lineStringMember>
                    <gml:LineString>
                        <gml:posList srsDimension="2">2 2 3 3</gml:posList>
                    </gml:LineString>
                </gml:lineStringMember>
            </gml:MultiLineString>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mls = result.Document!.Root.Should().BeOfType<GmlMultiLineString>().Subject;
        mls.LineStrings.Should().HaveCount(2);
    }

    // ---- MultiPolygon ----

    [Fact]
    public void ParseXmlString_WithMultiPolygon_ReturnsGmlMultiPolygon()
    {
        var xml = """
            <gml:MultiPolygon xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:polygonMember>
                    <gml:Polygon>
                        <gml:exterior>
                            <gml:LinearRing>
                                <gml:posList srsDimension="2">0 0 1 0 1 1 0 1 0 0</gml:posList>
                            </gml:LinearRing>
                        </gml:exterior>
                    </gml:Polygon>
                </gml:polygonMember>
            </gml:MultiPolygon>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mp = result.Document!.Root.Should().BeOfType<GmlMultiPolygon>().Subject;
        mp.Polygons.Should().HaveCount(1);
    }

    // ---- MultiCurve → MultiLineString ----

    [Fact]
    public void ParseXmlString_WithMultiCurve_ReturnsGmlMultiLineString()
    {
        var xml = """
            <gml:MultiCurve xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:curveMember>
                    <gml:LineString>
                        <gml:posList srsDimension="2">0 0 5 5</gml:posList>
                    </gml:LineString>
                </gml:curveMember>
            </gml:MultiCurve>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlMultiLineString>();
    }

    // ---- MultiSurface → MultiPolygon ----

    [Fact]
    public void ParseXmlString_WithMultiSurface_ReturnsGmlMultiPolygon()
    {
        var xml = """
            <gml:MultiSurface xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:surfaceMember>
                    <gml:Polygon>
                        <gml:exterior>
                            <gml:LinearRing>
                                <gml:posList srsDimension="2">0 0 1 0 1 1 0 1 0 0</gml:posList>
                            </gml:LinearRing>
                        </gml:exterior>
                    </gml:Polygon>
                </gml:surfaceMember>
            </gml:MultiSurface>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlMultiPolygon>();
    }

    // ---- Curve ----

    [Fact]
    public void ParseXmlString_WithCurve_ReturnsGmlCurve()
    {
        var xml = """
            <gml:Curve xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:segments>
                    <gml:LineStringSegment>
                        <gml:posList srsDimension="2">0 0 5 5 10 0</gml:posList>
                    </gml:LineStringSegment>
                </gml:segments>
            </gml:Curve>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var curve = result.Document!.Root.Should().BeOfType<GmlCurve>().Subject;
        curve.Coordinates.Should().HaveCount(3);
    }

    // ---- Surface ----

    [Fact]
    public void ParseXmlString_WithSurface_ReturnsGmlSurface()
    {
        var xml = """
            <gml:Surface xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:patches>
                    <gml:PolygonPatch>
                        <gml:exterior>
                            <gml:LinearRing>
                                <gml:posList srsDimension="2">0 0 10 0 10 10 0 10 0 0</gml:posList>
                            </gml:LinearRing>
                        </gml:exterior>
                    </gml:PolygonPatch>
                </gml:patches>
            </gml:Surface>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var surface = result.Document!.Root.Should().BeOfType<GmlSurface>().Subject;
        surface.Patches.Should().HaveCount(1);
        surface.Patches[0].Exterior.Coordinates.Should().HaveCount(5);
    }

    // ---- LinearRing (standalone) ----

    [Fact]
    public void ParseXmlString_WithLinearRing_ReturnsGmlLinearRing()
    {
        var xml = """
            <gml:LinearRing xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:posList srsDimension="2">0 0 10 0 10 10 0 10 0 0</gml:posList>
            </gml:LinearRing>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var ring = result.Document!.Root.Should().BeOfType<GmlLinearRing>().Subject;
        ring.Coordinates.Should().HaveCount(5);
    }
}

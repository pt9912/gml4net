using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class WktBuilderTests
{
    // ---- Point ----

    [Fact]
    public void Geometry_Point2D_ReturnsPoint()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(10.5, 20.3) };
        WktBuilder.Geometry(pt).Should().Be("POINT (10.5 20.3)");
    }

    [Fact]
    public void Geometry_Point3D_ReturnsPointZ()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2, 3) };
        WktBuilder.Geometry(pt).Should().Be("POINT Z (1 2 3)");
    }

    // ---- LineString ----

    [Fact]
    public void Geometry_LineString2D_ReturnsLineString()
    {
        var ls = new GmlLineString { Coordinates = [new(0, 0), new(1, 1), new(2, 0)] };
        WktBuilder.Geometry(ls).Should().Be("LINESTRING (0 0, 1 1, 2 0)");
    }

    [Fact]
    public void Geometry_LineString3D_ReturnsLineStringZ()
    {
        var ls = new GmlLineString { Coordinates = [new(0, 0, 0), new(1, 1, 1)] };
        WktBuilder.Geometry(ls).Should().Be("LINESTRING Z (0 0 0, 1 1 1)");
    }

    // ---- LinearRing → LineString ----

    [Fact]
    public void Geometry_LinearRing_ReturnsLineString()
    {
        var lr = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] };
        WktBuilder.Geometry(lr).Should().Be("LINESTRING (0 0, 1 0, 1 1, 0 0)");
    }

    // ---- Polygon ----

    [Fact]
    public void Geometry_Polygon2D_ReturnsPolygon()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(10, 0), new(10, 10), new(0, 0)] }
        };
        WktBuilder.Geometry(poly).Should().Be("POLYGON ((0 0, 10 0, 10 10, 0 0))");
    }

    [Fact]
    public void Geometry_PolygonWithHole_ReturnsPolygonWithRings()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(10, 0), new(10, 10), new(0, 0)] },
            Interior = [new GmlLinearRing { Coordinates = [new(1, 1), new(2, 1), new(2, 2), new(1, 1)] }]
        };
        var wkt = WktBuilder.Geometry(poly)!;

        wkt.Should().StartWith("POLYGON ((0 0,");
        wkt.Should().Contain("), (1 1,"); // hole ring
    }

    [Fact]
    public void Geometry_Polygon3D_ReturnsPolygonZ()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 0, 0)] }
        };
        WktBuilder.Geometry(poly).Should().StartWith("POLYGON Z ((");
    }

    // ---- Envelope / Box → Polygon ----

    [Fact]
    public void Geometry_Envelope_ReturnsPolygonRectangle()
    {
        var env = new GmlEnvelope
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(10, 10)
        };
        var wkt = WktBuilder.Geometry(env)!;

        wkt.Should().StartWith("POLYGON ((0 0,");
        wkt.Should().EndWith("0 0))");
    }

    [Fact]
    public void Geometry_Box_ReturnsPolygonRectangle()
    {
        var box = new GmlBox
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(5, 5)
        };
        WktBuilder.Geometry(box).Should().StartWith("POLYGON ((");
    }

    // ---- Curve → LineString ----

    [Fact]
    public void Geometry_Curve_ReturnsLineString()
    {
        var c = new GmlCurve { Coordinates = [new(0, 0), new(5, 5)] };
        WktBuilder.Geometry(c).Should().Be("LINESTRING (0 0, 5 5)");
    }

    // ---- Surface → MultiPolygon ----

    [Fact]
    public void Geometry_Surface_ReturnsMultiPolygon()
    {
        var s = new GmlSurface
        {
            Patches = [new GmlPolygon
            {
                Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] }
            }]
        };
        WktBuilder.Geometry(s).Should().StartWith("MULTIPOLYGON (((");
    }

    // ---- Multi types ----

    [Fact]
    public void Geometry_MultiPoint_ReturnsMultiPoint()
    {
        var mp = new GmlMultiPoint
        {
            Points = [new GmlPoint { Coordinate = new(1, 2) }, new GmlPoint { Coordinate = new(3, 4) }]
        };
        WktBuilder.Geometry(mp).Should().Be("MULTIPOINT ((1 2), (3 4))");
    }

    [Fact]
    public void Geometry_MultiPoint3D_ReturnsMultiPointZ()
    {
        var mp = new GmlMultiPoint
        {
            Points = [new GmlPoint { Coordinate = new(1, 2, 3) }, new GmlPoint { Coordinate = new(4, 5, 6) }]
        };
        WktBuilder.Geometry(mp).Should().Be("MULTIPOINT Z ((1 2 3), (4 5 6))");
    }

    [Fact]
    public void Geometry_MultiLineString_ReturnsMultiLineString()
    {
        var mls = new GmlMultiLineString
        {
            LineStrings = [
                new GmlLineString { Coordinates = [new(0, 0), new(1, 1)] },
                new GmlLineString { Coordinates = [new(2, 2), new(3, 3)] }
            ]
        };
        WktBuilder.Geometry(mls).Should().Be("MULTILINESTRING ((0 0, 1 1), (2 2, 3 3))");
    }

    [Fact]
    public void Geometry_MultiPolygon_ReturnsMultiPolygon()
    {
        var mp = new GmlMultiPolygon
        {
            Polygons = [new GmlPolygon
            {
                Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] }
            }]
        };
        WktBuilder.Geometry(mp).Should().StartWith("MULTIPOLYGON (((0 0,");
    }

    // ---- Null guard ----

    [Fact]
    public void Geometry_WithNull_ThrowsArgumentNullException()
    {
        var act = () => WktBuilder.Geometry(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---- EMPTY geometries ----

    [Fact]
    public void Geometry_EmptyLineString_ReturnsEmpty()
    {
        var ls = new GmlLineString { Coordinates = [] };
        WktBuilder.Geometry(ls).Should().Be("LINESTRING EMPTY");
    }

    [Fact]
    public void Geometry_EmptyCurve_ReturnsEmpty()
    {
        var c = new GmlCurve { Coordinates = [] };
        WktBuilder.Geometry(c).Should().Be("LINESTRING EMPTY");
    }

    // ---- M-Coordinate support ----

    [Fact]
    public void Geometry_PointWithZM_ReturnsPointZM()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2, 3, 4) };
        WktBuilder.Geometry(pt).Should().Be("POINT ZM (1 2 3 4)");
    }

    [Fact]
    public void Geometry_PointWithMOnly_ReturnsPointM()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2, M: 5) };
        WktBuilder.Geometry(pt).Should().Be("POINT M (1 2 5)");
    }

    // ---- 3D Polygon with interior ----

    [Fact]
    public void Geometry_PolygonWith3DInterior_UsesPolygonZ()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(10, 0), new(10, 10), new(0, 0)] },
            Interior = [new GmlLinearRing { Coordinates = [new(1, 1, 5), new(2, 1, 5), new(2, 2, 5), new(1, 1, 5)] }]
        };
        var wkt = WktBuilder.Geometry(poly)!;

        wkt.Should().StartWith("POLYGON Z (");
        // Exterior coords should have Z padded to 0
        wkt.Should().Contain("0 0 0");
    }

    // ---- Negative coordinates ----

    [Fact]
    public void Geometry_NegativeCoords_FormatsCorrectly()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(-122.4, -37.8) };
        WktBuilder.Geometry(pt).Should().Be("POINT (-122.4 -37.8)");
    }

    // ---- Envelope with Z ----

    [Fact]
    public void Geometry_EnvelopeWith3D_ReturnsPolygonZ()
    {
        var env = new GmlEnvelope
        {
            LowerCorner = new GmlCoordinate(0, 0, 100),
            UpperCorner = new GmlCoordinate(10, 10, 200)
        };
        var wkt = WktBuilder.Geometry(env)!;

        wkt.Should().StartWith("POLYGON Z ((");
    }

    // ---- Full roundtrip: GML XML → Parse → WKT ----

    [Fact]
    public void Roundtrip_ParseGmlThenConvertToWkt()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>11.5 48.1</gml:pos>
            </gml:Point>
            """;

        var result = Gml4Net.Parser.GmlParser.ParseXmlString(xml);
        var wkt = WktBuilder.Geometry((GmlGeometry)result.Document!.Root);

        wkt.Should().Be("POINT (11.5 48.1)");
    }
}

using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class KmlBuilderTests
{
    [Fact]
    public void Geometry_Point_ReturnsKmlPoint()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(11.5, 48.1) };
        var kml = KmlBuilder.Geometry(pt)!;

        kml.Name.LocalName.Should().Be("Point");
        kml.ToString().Should().Contain("11.5,48.1");
    }

    [Fact]
    public void Geometry_Point3D_IncludesAltitude()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(11.5, 48.1, 500) };
        var kml = KmlBuilder.Geometry(pt)!;

        kml.ToString().Should().Contain("11.5,48.1,500");
    }

    [Fact]
    public void Geometry_LineString_ReturnsKmlLineString()
    {
        var ls = new GmlLineString { Coordinates = [new(0, 0), new(1, 1), new(2, 0)] };
        var kml = KmlBuilder.Geometry(ls)!;

        kml.Name.LocalName.Should().Be("LineString");
        kml.ToString().Should().Contain("0,0 1,1 2,0");
    }

    [Fact]
    public void Geometry_Polygon_ReturnsKmlPolygon()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] },
            Interior = [new GmlLinearRing { Coordinates = [new(0.1, 0.1), new(0.2, 0.1), new(0.2, 0.2), new(0.1, 0.1)] }]
        };
        var kml = KmlBuilder.Geometry(poly)!;

        kml.Name.LocalName.Should().Be("Polygon");
        kml.ToString().Should().Contain("outerBoundaryIs");
        kml.ToString().Should().Contain("innerBoundaryIs");
    }

    [Fact]
    public void Geometry_Envelope_ReturnsKmlPolygon()
    {
        var env = new GmlEnvelope
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(10, 10)
        };
        var kml = KmlBuilder.Geometry(env)!;

        kml.Name.LocalName.Should().Be("Polygon");
    }

    [Fact]
    public void Geometry_MultiPoint_ReturnsMultiGeometry()
    {
        var mp = new GmlMultiPoint
        {
            Points = [new GmlPoint { Coordinate = new(1, 2) }, new GmlPoint { Coordinate = new(3, 4) }]
        };
        var kml = KmlBuilder.Geometry(mp)!;

        kml.Name.LocalName.Should().Be("MultiGeometry");
    }

    [Fact]
    public void Geometry_Curve_ReturnsLineString()
    {
        var c = new GmlCurve { Coordinates = [new(0, 0), new(5, 5)] };
        var kml = KmlBuilder.Geometry(c)!;

        kml.Name.LocalName.Should().Be("LineString");
    }

    [Fact]
    public void Geometry_Surface_ReturnsMultiGeometry()
    {
        var s = new GmlSurface
        {
            Patches = [new GmlPolygon
            {
                Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] }
            }]
        };
        var kml = KmlBuilder.Geometry(s)!;

        kml.Name.LocalName.Should().Be("MultiGeometry");
    }

    [Fact]
    public void Feature_ReturnsPlacemark()
    {
        var feature = new GmlFeature
        {
            Id = "building.1",
            Properties = new GmlPropertyBag(
            [
                new GmlPropertyEntry { Name = "name", Value = new GmlStringProperty { Value = "Town Hall" } },
                new GmlPropertyEntry { Name = "height", Value = new GmlNumericProperty { Value = 42 } },
                new GmlPropertyEntry
                {
                    Name = "geom",
                    Value = new GmlGeometryProperty
                    {
                        Geometry = new GmlPoint { Coordinate = new(11.5, 48.1) }
                    }
                }
            ])
        };
        var kml = KmlBuilder.Feature(feature);

        kml.Name.LocalName.Should().Be("Placemark");
        kml.ToString().Should().Contain("building.1");
        kml.ToString().Should().Contain("Town Hall");
        kml.ToString().Should().Contain("Point");
    }

    [Fact]
    public void FeatureCollection_ReturnsKmlDocument()
    {
        var fc = new GmlFeatureCollection
        {
            Features = [new GmlFeature { Id = "a" }, new GmlFeature { Id = "b" }]
        };
        var kml = KmlBuilder.FeatureCollection(fc);

        kml.Name.LocalName.Should().Be("kml");
        kml.ToString().Should().Contain("Document");
        kml.ToString().Should().Contain("Placemark");
    }

    [Fact]
    public void GeometryToKml_ReturnsString()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var str = KmlBuilder.GeometryToKml(pt);

        str.Should().Contain("Point");
        str.Should().Contain("1,2");
    }

    [Fact]
    public void Geometry_LinearRing_ReturnsKmlLinearRing()
    {
        var lr = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] };
        var kml = KmlBuilder.Geometry(lr)!;

        kml.Name.LocalName.Should().Be("LinearRing");
    }

    [Fact]
    public void Geometry_Box_ReturnsKmlPolygon()
    {
        var box = new GmlBox
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(5, 5)
        };
        var kml = KmlBuilder.Geometry(box)!;

        kml.Name.LocalName.Should().Be("Polygon");
    }

    [Fact]
    public void Geometry_MultiLineString_ReturnsMultiGeometry()
    {
        var mls = new GmlMultiLineString
        {
            LineStrings = [new GmlLineString { Coordinates = [new(0, 0), new(1, 1)] }]
        };
        var kml = KmlBuilder.Geometry(mls)!;

        kml.Name.LocalName.Should().Be("MultiGeometry");
    }

    [Fact]
    public void Geometry_MultiPolygon_ReturnsMultiGeometry()
    {
        var mp = new GmlMultiPolygon
        {
            Polygons = [new GmlPolygon
            {
                Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] }
            }]
        };
        var kml = KmlBuilder.Geometry(mp)!;

        kml.Name.LocalName.Should().Be("MultiGeometry");
    }

    // ---- Roundtrip ----

    [Fact]
    public void Roundtrip_ParseGmlThenConvertToKml()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>11.5 48.1</gml:pos>
            </gml:Point>
            """;

        var result = Gml4Net.Parser.GmlParser.ParseXmlString(xml);
        var kml = KmlBuilder.Geometry((GmlGeometry)result.Document!.Root)!;

        kml.Name.LocalName.Should().Be("Point");
        kml.ToString().Should().Contain("11.5,48.1");
    }
}

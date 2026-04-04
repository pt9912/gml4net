using System.Text.Json.Nodes;
using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class GeoJsonBuilderTests
{
    [Fact]
    public void Geometry_Point_ReturnsGeoJsonPoint()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(10.5, 20.3) };
        var json = GeoJsonBuilder.Geometry(pt)!;

        json["type"]!.GetValue<string>().Should().Be("Point");
        var coords = json["coordinates"]!.AsArray();
        coords[0]!.GetValue<double>().Should().Be(10.5);
        coords[1]!.GetValue<double>().Should().Be(20.3);
    }

    [Fact]
    public void Geometry_Point3D_IncludesZ()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2, 3) };
        var json = GeoJsonBuilder.Geometry(pt)!;

        json["coordinates"]!.AsArray().Should().HaveCount(3);
    }

    [Fact]
    public void Geometry_LineString_ReturnsGeoJsonLineString()
    {
        var ls = new GmlLineString { Coordinates = [new(0, 0), new(1, 1), new(2, 2)] };
        var json = GeoJsonBuilder.Geometry(ls)!;

        json["type"]!.GetValue<string>().Should().Be("LineString");
        json["coordinates"]!.AsArray().Should().HaveCount(3);
    }

    [Fact]
    public void Geometry_Polygon_ReturnsGeoJsonPolygon()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)] },
            Interior = [new GmlLinearRing { Coordinates = [new(1, 1), new(2, 1), new(2, 2), new(1, 2), new(1, 1)] }]
        };
        var json = GeoJsonBuilder.Geometry(poly)!;

        json["type"]!.GetValue<string>().Should().Be("Polygon");
        var rings = json["coordinates"]!.AsArray();
        rings.Should().HaveCount(2); // exterior + 1 hole
    }

    [Fact]
    public void Geometry_Envelope_ReturnsPolygonRectangle()
    {
        var env = new GmlEnvelope
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(10, 10)
        };
        var json = GeoJsonBuilder.Geometry(env)!;

        json["type"]!.GetValue<string>().Should().Be("Polygon");
        var ring = json["coordinates"]!.AsArray()[0]!.AsArray();
        ring.Should().HaveCount(5); // closed rectangle
    }

    [Fact]
    public void Geometry_Box_ReturnsPolygonRectangle()
    {
        var box = new GmlBox
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(5, 5)
        };
        var json = GeoJsonBuilder.Geometry(box)!;

        json["type"]!.GetValue<string>().Should().Be("Polygon");
    }

    [Fact]
    public void Geometry_Curve_ReturnsLineString()
    {
        var curve = new GmlCurve { Coordinates = [new(0, 0), new(5, 5), new(10, 0)] };
        var json = GeoJsonBuilder.Geometry(curve)!;

        json["type"]!.GetValue<string>().Should().Be("LineString");
        json["coordinates"]!.AsArray().Should().HaveCount(3);
    }

    [Fact]
    public void Geometry_Surface_ReturnsMultiPolygon()
    {
        var surface = new GmlSurface
        {
            Patches = [new GmlPolygon
            {
                Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] }
            }]
        };
        var json = GeoJsonBuilder.Geometry(surface)!;

        json["type"]!.GetValue<string>().Should().Be("MultiPolygon");
    }

    [Fact]
    public void Geometry_MultiPoint_ReturnsGeoJsonMultiPoint()
    {
        var mp = new GmlMultiPoint
        {
            Points = [new GmlPoint { Coordinate = new(1, 2) }, new GmlPoint { Coordinate = new(3, 4) }]
        };
        var json = GeoJsonBuilder.Geometry(mp)!;

        json["type"]!.GetValue<string>().Should().Be("MultiPoint");
        json["coordinates"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public void Geometry_MultiLineString_ReturnsGeoJsonMultiLineString()
    {
        var mls = new GmlMultiLineString
        {
            LineStrings = [new GmlLineString { Coordinates = [new(0, 0), new(1, 1)] }]
        };
        var json = GeoJsonBuilder.Geometry(mls)!;

        json["type"]!.GetValue<string>().Should().Be("MultiLineString");
    }

    [Fact]
    public void Geometry_MultiPolygon_ReturnsGeoJsonMultiPolygon()
    {
        var mp = new GmlMultiPolygon
        {
            Polygons = [new GmlPolygon
            {
                Exterior = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] }
            }]
        };
        var json = GeoJsonBuilder.Geometry(mp)!;

        json["type"]!.GetValue<string>().Should().Be("MultiPolygon");
    }

    [Fact]
    public void Geometry_LinearRing_ReturnsLineString()
    {
        var lr = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] };
        var json = GeoJsonBuilder.Geometry(lr)!;

        json["type"]!.GetValue<string>().Should().Be("LineString");
    }

    // ---- Feature ----

    [Fact]
    public void Feature_WithGeometryAndProperties_ReturnsGeoJsonFeature()
    {
        var feature = new GmlFeature
        {
            Id = "f.1",
            Properties = new Dictionary<string, GmlPropertyValue>
            {
                ["name"] = new GmlStringProperty { Value = "Test" },
                ["value"] = new GmlNumericProperty { Value = 42 },
                ["geom"] = new GmlGeometryProperty
                {
                    Geometry = new GmlPoint { Coordinate = new(1, 2) }
                }
            }
        };
        var json = GeoJsonBuilder.Feature(feature);

        json["type"]!.GetValue<string>().Should().Be("Feature");
        json["id"]!.GetValue<string>().Should().Be("f.1");
        json["geometry"]!["type"]!.GetValue<string>().Should().Be("Point");
        json["properties"]!["name"]!.GetValue<string>().Should().Be("Test");
        json["properties"]!["value"]!.GetValue<long>().Should().Be(42);
    }

    [Fact]
    public void Feature_WithNestedProperty_ConvertsToNestedJson()
    {
        var feature = new GmlFeature
        {
            Properties = new Dictionary<string, GmlPropertyValue>
            {
                ["addr"] = new GmlNestedProperty
                {
                    Children = new Dictionary<string, GmlPropertyValue>
                    {
                        ["city"] = new GmlStringProperty { Value = "Berlin" }
                    }
                }
            }
        };
        var json = GeoJsonBuilder.Feature(feature);

        json["properties"]!["addr"]!["city"]!.GetValue<string>().Should().Be("Berlin");
    }

    [Fact]
    public void Feature_WithRawXmlProperty_ConvertsToString()
    {
        var feature = new GmlFeature
        {
            Properties = new Dictionary<string, GmlPropertyValue>
            {
                ["data"] = new GmlRawXmlProperty { XmlContent = "<x>raw</x>" }
            }
        };
        var json = GeoJsonBuilder.Feature(feature);

        json["properties"]!["data"]!.GetValue<string>().Should().Be("<x>raw</x>");
    }

    // ---- FeatureCollection ----

    [Fact]
    public void FeatureCollection_ReturnsGeoJsonFeatureCollection()
    {
        var fc = new GmlFeatureCollection
        {
            Features = [new GmlFeature { Id = "a" }, new GmlFeature { Id = "b" }]
        };
        var json = GeoJsonBuilder.FeatureCollection(fc);

        json["type"]!.GetValue<string>().Should().Be("FeatureCollection");
        json["features"]!.AsArray().Should().HaveCount(2);
    }

    // ---- Document dispatch ----

    [Fact]
    public void Document_WithGeometry_ReturnsGeometry()
    {
        var doc = new GmlDocument
        {
            Version = GmlVersion.V3_2,
            Root = new GmlPoint { Coordinate = new(1, 2) }
        };
        var json = GeoJsonBuilder.Document(doc)!;

        json["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void Document_WithFeatureCollection_ReturnsFeatureCollection()
    {
        var doc = new GmlDocument
        {
            Version = GmlVersion.V3_2,
            Root = new GmlFeatureCollection { Features = [] }
        };
        var json = GeoJsonBuilder.Document(doc)!;

        json["type"]!.GetValue<string>().Should().Be("FeatureCollection");
    }

    // ---- String variants ----

    [Fact]
    public void GeometryToJson_ReturnsValidJsonString()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var jsonStr = GeoJsonBuilder.GeometryToJson(pt);

        jsonStr.Should().Contain("\"type\":\"Point\"");
        jsonStr.Should().Contain("\"coordinates\":[1,2]");
    }

    [Fact]
    public void FeatureToJson_ReturnsValidJsonString()
    {
        var feature = new GmlFeature();
        var jsonStr = GeoJsonBuilder.FeatureToJson(feature);

        jsonStr.Should().Contain("\"type\":\"Feature\"");
    }

    [Fact]
    public void FeatureCollectionToJson_ReturnsValidJsonString()
    {
        var fc = new GmlFeatureCollection();
        var jsonStr = GeoJsonBuilder.FeatureCollectionToJson(fc);

        jsonStr.Should().Contain("\"type\":\"FeatureCollection\"");
    }

    // ---- Full roundtrip: GML XML → Parse → GeoJSON ----

    [Fact]
    public void Roundtrip_ParseGmlThenConvertToGeoJson()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:City gml:id="city.1">
                        <app:name>Munich</app:name>
                        <app:population>1500000</app:population>
                        <app:location>
                            <gml:Point><gml:pos>11.5 48.1</gml:pos></gml:Point>
                        </app:location>
                    </app:City>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);
        var json = GeoJsonBuilder.Document(result.Document!)!;

        json["type"]!.GetValue<string>().Should().Be("FeatureCollection");
        var feature = json["features"]![0]!;
        feature["geometry"]!["type"]!.GetValue<string>().Should().Be("Point");
        feature["properties"]!["name"]!.GetValue<string>().Should().Be("Munich");
    }

    // ---- Integer coordinate formatting ----

    [Fact]
    public void Geometry_WithIntegerCoords_FormatsAsIntegers()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(10, 20) };
        var jsonStr = GeoJsonBuilder.GeometryToJson(pt)!;

        // Should be [10,20] not [10.0,20.0]
        jsonStr.Should().Contain("[10,20]");
    }

    // ---- Null guards ----

    [Fact]
    public void Geometry_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GeoJsonBuilder.Geometry(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Feature_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GeoJsonBuilder.Feature(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FeatureCollection_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GeoJsonBuilder.FeatureCollection(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Document_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GeoJsonBuilder.Document(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Feature without geometry ----

    [Fact]
    public void Feature_WithoutGeometry_HasNullGeometry()
    {
        var feature = new GmlFeature
        {
            Properties = new Dictionary<string, GmlPropertyValue>
            {
                ["name"] = new GmlStringProperty { Value = "no-geom" }
            }
        };
        var json = GeoJsonBuilder.Feature(feature);
        var jsonStr = json.ToJsonString();

        jsonStr.Should().Contain("\"geometry\":null");
        json["properties"]!["name"]!.GetValue<string>().Should().Be("no-geom");
    }

    // ---- Feature without ID omits id field ----

    [Fact]
    public void Feature_WithoutId_OmitsIdField()
    {
        var feature = new GmlFeature();
        var json = GeoJsonBuilder.Feature(feature);

        json.ContainsKey("id").Should().BeFalse();
    }

    // ---- Document with single Feature ----

    [Fact]
    public void Document_WithSingleFeature_ReturnsFeature()
    {
        var doc = new GmlDocument
        {
            Version = GmlVersion.V3_2,
            Root = new GmlFeature { Id = "f.1" }
        };
        var json = GeoJsonBuilder.Document(doc)!;

        json["type"]!.GetValue<string>().Should().Be("Feature");
        json["id"]!.GetValue<string>().Should().Be("f.1");
    }

    // ---- M-Coordinate support ----

    [Fact]
    public void Geometry_PointWithM_IncludesMInCoordinates()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2, 3, 4) };
        var json = GeoJsonBuilder.Geometry(pt)!;

        json["coordinates"]!.AsArray().Should().HaveCount(4);
    }

    [Fact]
    public void Geometry_PointWithMButNoZ_PreservesThreeOrdinatePosition()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2, M: 5) };
        var json = GeoJsonBuilder.Geometry(pt)!;

        var coords = json["coordinates"]!.AsArray();
        coords.Should().HaveCount(3);
        coords[2]!.GetValue<long>().Should().Be(5);
    }

    [Fact]
    public void Geometry_LineStringWithMOnly_PreservesThreeOrdinatePositions()
    {
        var line = new GmlLineString { Coordinates = [new(1, 2, M: 3), new(4, 5, M: 6)] };
        var json = GeoJsonBuilder.Geometry(line)!;

        var coords = json["coordinates"]!.AsArray();
        coords.Should().HaveCount(2);
        coords[0]!.AsArray().Should().HaveCount(3);
        coords[0]![2]!.GetValue<long>().Should().Be(3);
    }

    // ---- Negative coordinates ----

    [Fact]
    public void Geometry_NegativeCoords_FormatsCorrectly()
    {
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(-122.4, -37.8) };
        var jsonStr = GeoJsonBuilder.GeometryToJson(pt)!;

        jsonStr.Should().Contain("-122.4");
        jsonStr.Should().Contain("-37.8");
    }
}

using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class IGmlBuilderTests
{
    [Fact]
    public void GeoJsonBuilder_ImplementsIGmlBuilder()
    {
        IGmlBuilder<JsonObject, JsonObject, JsonObject> builder = GeoJsonBuilder.Instance;

        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var result = builder.BuildPoint(pt);

        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void WktBuilder_ImplementsIGmlBuilder()
    {
        IGmlBuilder<string, string, string> builder = WktBuilder.Instance;

        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var result = builder.BuildPoint(pt);

        result.Should().Be("POINT (1 2)");
    }

    [Fact]
    public void KmlBuilder_ImplementsIGmlBuilder()
    {
        IGmlBuilder<XElement, XElement, XElement> builder = KmlBuilder.Instance;

        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var result = builder.BuildPoint(pt);

        result.Should().NotBeNull();
        result!.Name.LocalName.Should().Be("Point");
    }

    [Fact]
    public void AllBuilders_BuildFeature_ViaInterface()
    {
        var feature = new GmlFeature
        {
            Id = "f.1",
            Properties = new GmlPropertyBag(
            [
                new GmlPropertyEntry { Name = "name", Value = new GmlStringProperty { Value = "Test" } },
                new GmlPropertyEntry
                {
                    Name = "geom",
                    Value = new GmlGeometryProperty { Geometry = new GmlPoint { Coordinate = new(1, 2) } }
                }
            ])
        };

        // GeoJSON
        IGmlBuilder<JsonObject, JsonObject, JsonObject> geojson = GeoJsonBuilder.Instance;
        var gj = geojson.BuildFeature(feature);
        gj["type"]!.GetValue<string>().Should().Be("Feature");

        // WKT
        IGmlBuilder<string, string, string> wkt = WktBuilder.Instance;
        var w = wkt.BuildFeature(feature);
        w.Should().Contain("POINT");

        // KML
        IGmlBuilder<XElement, XElement, XElement> kml = KmlBuilder.Instance;
        var k = kml.BuildFeature(feature);
        k.Name.LocalName.Should().Be("Placemark");
    }

    [Fact]
    public void AllBuilders_BuildFeatureCollection_ViaInterface()
    {
        var fc = new GmlFeatureCollection
        {
            Features = [new GmlFeature { Id = "a" }]
        };

        var gj = GeoJsonBuilder.Instance.BuildFeatureCollection(fc);
        gj["type"]!.GetValue<string>().Should().Be("FeatureCollection");

        var wkt = WktBuilder.Instance.BuildFeatureCollection(fc);
        wkt.Should().NotBeNull();

        var kml = KmlBuilder.Instance.BuildFeatureCollection(fc);
        kml.Name.LocalName.Should().Be("kml");
    }

    [Fact]
    public void AllBuilders_BuildAllGeometryTypes_ViaInterface()
    {
        var pt = new GmlPoint { Coordinate = new(1, 2) };
        var ls = new GmlLineString { Coordinates = [new(0, 0), new(1, 1)] };
        var lr = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] };
        var poly = new GmlPolygon { Exterior = lr };
        var env = new GmlEnvelope { LowerCorner = new(0, 0), UpperCorner = new(1, 1) };
        var box = new GmlBox { LowerCorner = new(0, 0), UpperCorner = new(1, 1) };
        var curve = new GmlCurve { Coordinates = [new(0, 0), new(1, 1)] };
        var surface = new GmlSurface { Patches = [poly] };
        var mp = new GmlMultiPoint { Points = [pt] };
        var mls = new GmlMultiLineString { LineStrings = [ls] };
        var mpoly = new GmlMultiPolygon { Polygons = [poly] };

        // Verify all three builders handle all geometry types without throwing
        foreach (var builder in new object[] { GeoJsonBuilder.Instance, WktBuilder.Instance, KmlBuilder.Instance })
        {
            if (builder is IGmlBuilder<JsonObject, JsonObject, JsonObject> gj)
            {
                gj.BuildPoint(pt).Should().NotBeNull();
                gj.BuildLineString(ls).Should().NotBeNull();
                gj.BuildLinearRing(lr).Should().NotBeNull();
                gj.BuildPolygon(poly).Should().NotBeNull();
                gj.BuildEnvelope(env).Should().NotBeNull();
                gj.BuildBox(box).Should().NotBeNull();
                gj.BuildCurve(curve).Should().NotBeNull();
                gj.BuildSurface(surface).Should().NotBeNull();
                gj.BuildMultiPoint(mp).Should().NotBeNull();
                gj.BuildMultiLineString(mls).Should().NotBeNull();
                gj.BuildMultiPolygon(mpoly).Should().NotBeNull();
            }
            else if (builder is IGmlBuilder<string, string, string> wkt)
            {
                wkt.BuildPoint(pt).Should().NotBeNull();
                wkt.BuildLineString(ls).Should().NotBeNull();
                wkt.BuildLinearRing(lr).Should().NotBeNull();
                wkt.BuildPolygon(poly).Should().NotBeNull();
                wkt.BuildEnvelope(env).Should().NotBeNull();
                wkt.BuildBox(box).Should().NotBeNull();
                wkt.BuildCurve(curve).Should().NotBeNull();
                wkt.BuildSurface(surface).Should().NotBeNull();
                wkt.BuildMultiPoint(mp).Should().NotBeNull();
                wkt.BuildMultiLineString(mls).Should().NotBeNull();
                wkt.BuildMultiPolygon(mpoly).Should().NotBeNull();
            }
            else if (builder is IGmlBuilder<XElement, XElement, XElement> kml)
            {
                kml.BuildPoint(pt).Should().NotBeNull();
                kml.BuildLineString(ls).Should().NotBeNull();
                kml.BuildLinearRing(lr).Should().NotBeNull();
                kml.BuildPolygon(poly).Should().NotBeNull();
                kml.BuildEnvelope(env).Should().NotBeNull();
                kml.BuildBox(box).Should().NotBeNull();
                kml.BuildCurve(curve).Should().NotBeNull();
                kml.BuildSurface(surface).Should().NotBeNull();
                kml.BuildMultiPoint(mp).Should().NotBeNull();
                kml.BuildMultiLineString(mls).Should().NotBeNull();
                kml.BuildMultiPolygon(mpoly).Should().NotBeNull();
            }
        }
    }
}

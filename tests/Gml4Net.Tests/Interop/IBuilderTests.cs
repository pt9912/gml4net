using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class IBuilderTests
{
    [Fact]
    public void GeoJsonBuilder_ImplementsIBuilder()
    {
        IBuilder<JsonObject, JsonObject, JsonObject> builder = GeoJsonBuilder.Instance;

        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var result = builder.BuildPoint(pt);

        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be("Point");
    }

    [Fact]
    public void WktBuilder_ImplementsIBuilder()
    {
        IBuilder<string, string, string> builder = WktBuilder.Instance;

        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };
        var result = builder.BuildPoint(pt);

        result.Should().Be("POINT (1 2)");
    }

    [Fact]
    public void KmlBuilder_ImplementsIBuilder()
    {
        IBuilder<XElement, XElement, XElement> builder = KmlBuilder.Instance;

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
        IBuilder<JsonObject, JsonObject, JsonObject> geojson = GeoJsonBuilder.Instance;
        var gj = geojson.BuildFeature(feature);
        gj["type"]!.GetValue<string>().Should().Be("Feature");

        // WKT
        IBuilder<string, string, string> wkt = WktBuilder.Instance;
        var w = wkt.BuildFeature(feature);
        w.Should().Contain("POINT");

        // KML
        IBuilder<XElement, XElement, XElement> kml = KmlBuilder.Instance;
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
    public void GeoJsonBuilder_AllGeometryTypes_ViaInterface()
    {
        IBuilder<JsonObject, JsonObject, JsonObject> b = GeoJsonBuilder.Instance;
        AssertAllGeometryMethodsNotNull(b);
    }

    [Fact]
    public void WktBuilder_AllGeometryTypes_ViaInterface()
    {
        IBuilder<string, string, string> b = WktBuilder.Instance;
        AssertAllGeometryMethodsNotNull(b);
    }

    [Fact]
    public void KmlBuilder_AllGeometryTypes_ViaInterface()
    {
        IBuilder<XElement, XElement, XElement> b = KmlBuilder.Instance;
        AssertAllGeometryMethodsNotNull(b);
    }

    [Fact]
    public void GeoJsonBuilder_BuildPoint_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GeoJsonBuilder.Instance.BuildPoint(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WktBuilder_BuildPoint_WithNull_ThrowsArgumentNullException()
    {
        var act = () => WktBuilder.Instance.BuildPoint(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void KmlBuilder_BuildPoint_WithNull_ThrowsArgumentNullException()
    {
        var act = () => KmlBuilder.Instance.BuildPoint(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AllBuilders_BuildCoverage_ReturnsNull()
    {
        var coverage = new Gml4Net.Model.Coverage.GmlMultiPointCoverage();

        GeoJsonBuilder.Instance.BuildCoverage(coverage).Should().BeNull();
        KmlBuilder.Instance.BuildCoverage(coverage).Should().BeNull();
        WktBuilder.Instance.BuildCoverage(coverage).Should().BeNull();
    }

    private static void AssertAllGeometryMethodsNotNull<TG, TF, TC>(IBuilder<TG, TF, TC> b)
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

        b.BuildPoint(pt).Should().NotBeNull();
        b.BuildLineString(ls).Should().NotBeNull();
        b.BuildLinearRing(lr).Should().NotBeNull();
        b.BuildPolygon(poly).Should().NotBeNull();
        b.BuildEnvelope(env).Should().NotBeNull();
        b.BuildBox(box).Should().NotBeNull();
        b.BuildCurve(curve).Should().NotBeNull();
        b.BuildSurface(surface).Should().NotBeNull();
        b.BuildMultiPoint(mp).Should().NotBeNull();
        b.BuildMultiLineString(mls).Should().NotBeNull();
        b.BuildMultiPolygon(mpoly).Should().NotBeNull();
    }
}

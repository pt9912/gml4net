using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class BuilderExtensionsTests
{
    [Theory]
    [MemberData(nameof(AllGeometryTypes))]
    public void BuildGeometry_DispatchesAllTypes_GeoJson(GmlGeometry geometry, string expectedType)
    {
        IBuilder<JsonObject, JsonObject, JsonObject> builder = GeoJsonBuilder.Instance;

        var result = builder.BuildGeometry(geometry);

        result.Should().NotBeNull();
        result!["type"]!.GetValue<string>().Should().Be(expectedType);
    }

    [Theory]
    [MemberData(nameof(AllGeometryTypes))]
    public void BuildGeometry_DispatchesAllTypes_Wkt(GmlGeometry geometry, string _)
    {
        IBuilder<string, string, string> builder = WktBuilder.Instance;

        var result = builder.BuildGeometry(geometry);

        result.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [MemberData(nameof(AllGeometryTypes))]
    public void BuildGeometry_DispatchesAllTypes_Kml(GmlGeometry geometry, string _)
    {
        IBuilder<XElement, XElement, XElement> builder = KmlBuilder.Instance;

        var result = builder.BuildGeometry(geometry);

        result.Should().NotBeNull();
    }

    [Fact]
    public void BuildGeometry_WithNullBuilder_ThrowsArgumentNullException()
    {
        IBuilder<string, string, string> builder = null!;
        var pt = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) };

        var act = () => builder.BuildGeometry(pt);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildGeometry_WithNullGeometry_ThrowsArgumentNullException()
    {
        var act = () => GeoJsonBuilder.Instance.BuildGeometry(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    public static TheoryData<GmlGeometry, string> AllGeometryTypes()
    {
        var lr = new GmlLinearRing { Coordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 0)] };
        var poly = new GmlPolygon { Exterior = lr };

        return new TheoryData<GmlGeometry, string>
        {
            { new GmlPoint { Coordinate = new(1, 2) }, "Point" },
            { new GmlLineString { Coordinates = [new(0, 0), new(1, 1)] }, "LineString" },
            { lr, "LineString" },
            { poly, "Polygon" },
            { new GmlEnvelope { LowerCorner = new(0, 0), UpperCorner = new(1, 1) }, "Polygon" },
            { new GmlBox { LowerCorner = new(0, 0), UpperCorner = new(1, 1) }, "Polygon" },
            { new GmlCurve { Coordinates = [new(0, 0), new(1, 1)] }, "LineString" },
            { new GmlSurface { Patches = [poly] }, "MultiPolygon" },
            { new GmlMultiPoint { Points = [new GmlPoint { Coordinate = new(1, 2) }] }, "MultiPoint" },
            { new GmlMultiLineString { LineStrings = [new GmlLineString { Coordinates = [new(0, 0), new(1, 1)] }] }, "MultiLineString" },
            { new GmlMultiPolygon { Polygons = [poly] }, "MultiPolygon" }
        };
    }
}

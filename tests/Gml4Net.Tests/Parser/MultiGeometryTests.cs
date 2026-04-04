using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

/// <summary>
/// Tests for MultiGeometry aggregation including homogeneous and heterogeneous cases.
/// </summary>
public class MultiGeometryTests
{
    [Fact]
    public void ParseXmlString_WithMultiGeometryAllPoints_ReturnsMultiPoint()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:geometryMember>
                    <gml:Point><gml:pos>1 2</gml:pos></gml:Point>
                </gml:geometryMember>
                <gml:geometryMember>
                    <gml:Point><gml:pos>3 4</gml:pos></gml:Point>
                </gml:geometryMember>
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mp = result.Document!.Root.Should().BeOfType<GmlMultiPoint>().Subject;
        mp.Points.Should().HaveCount(2);
    }

    [Fact]
    public void ParseXmlString_WithMultiGeometryAllLineStrings_ReturnsMultiLineString()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:geometryMember>
                    <gml:LineString><gml:posList srsDimension="2">0 0 1 1</gml:posList></gml:LineString>
                </gml:geometryMember>
                <gml:geometryMember>
                    <gml:LineString><gml:posList srsDimension="2">2 2 3 3</gml:posList></gml:LineString>
                </gml:geometryMember>
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mls = result.Document!.Root.Should().BeOfType<GmlMultiLineString>().Subject;
        mls.LineStrings.Should().HaveCount(2);
    }

    [Fact]
    public void ParseXmlString_WithMultiGeometryAllPolygons_ReturnsMultiPolygon()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:geometryMember>
                    <gml:Polygon>
                        <gml:exterior><gml:LinearRing>
                            <gml:posList srsDimension="2">0 0 1 0 1 1 0 1 0 0</gml:posList>
                        </gml:LinearRing></gml:exterior>
                    </gml:Polygon>
                </gml:geometryMember>
                <gml:geometryMember>
                    <gml:Polygon>
                        <gml:exterior><gml:LinearRing>
                            <gml:posList srsDimension="2">2 2 3 2 3 3 2 3 2 2</gml:posList>
                        </gml:LinearRing></gml:exterior>
                    </gml:Polygon>
                </gml:geometryMember>
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlMultiPolygon>();
    }

    [Fact]
    public void ParseXmlString_WithMultiGeometryMixed_ReturnsWarningAndDominantType()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:geometryMember>
                    <gml:Point><gml:pos>1 2</gml:pos></gml:Point>
                </gml:geometryMember>
                <gml:geometryMember>
                    <gml:LineString><gml:posList srsDimension="2">0 0 1 1</gml:posList></gml:LineString>
                </gml:geometryMember>
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.Issues.Should().Contain(i => i.Code == "heterogeneous_multi_geometry");
        result.Document.Should().NotBeNull();
    }

    [Fact]
    public void ParseXmlString_WithMultiGeometrySingleMember_ReturnsSingleGeometry()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:geometryMember>
                    <gml:Point><gml:pos>5 6</gml:pos></gml:Point>
                </gml:geometryMember>
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlPoint>();
    }

    [Fact]
    public void ParseXmlString_WithMultiGeometryEmpty_ReturnsError()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void ParseXmlString_WithMultiGeometryUsingGeometryMembers_ParsesCorrectly()
    {
        var xml = """
            <gml:MultiGeometry xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:geometryMembers>
                    <gml:Point><gml:pos>1 2</gml:pos></gml:Point>
                    <gml:Point><gml:pos>3 4</gml:pos></gml:Point>
                </gml:geometryMembers>
            </gml:MultiGeometry>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mp = result.Document!.Root.Should().BeOfType<GmlMultiPoint>().Subject;
        mp.Points.Should().HaveCount(2);
    }

    // ---- MultiCurve with actual Curve children ----

    [Fact]
    public void ParseXmlString_WithMultiCurveContainingCurve_FlattensToLineStrings()
    {
        var xml = """
            <gml:MultiCurve xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:curveMember>
                    <gml:Curve>
                        <gml:segments>
                            <gml:LineStringSegment>
                                <gml:posList srsDimension="2">0 0 5 5</gml:posList>
                            </gml:LineStringSegment>
                        </gml:segments>
                    </gml:Curve>
                </gml:curveMember>
            </gml:MultiCurve>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mls = result.Document!.Root.Should().BeOfType<GmlMultiLineString>().Subject;
        mls.LineStrings.Should().HaveCount(1);
        mls.LineStrings[0].Coordinates.Should().HaveCount(2);
    }

    // ---- MultiSurface with Surface children ----

    [Fact]
    public void ParseXmlString_WithMultiSurfaceContainingSurface_FlattensToPolygons()
    {
        var xml = """
            <gml:MultiSurface xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:surfaceMember>
                    <gml:Surface>
                        <gml:patches>
                            <gml:PolygonPatch>
                                <gml:exterior><gml:LinearRing>
                                    <gml:posList srsDimension="2">0 0 1 0 1 1 0 1 0 0</gml:posList>
                                </gml:LinearRing></gml:exterior>
                            </gml:PolygonPatch>
                        </gml:patches>
                    </gml:Surface>
                </gml:surfaceMember>
            </gml:MultiSurface>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mp = result.Document!.Root.Should().BeOfType<GmlMultiPolygon>().Subject;
        mp.Polygons.Should().HaveCount(1);
    }

    // ---- Curve with multiple segments ----

    [Fact]
    public void ParseXmlString_WithCurveMultipleSegments_DeduplicatesBoundaryPoints()
    {
        var xml = """
            <gml:Curve xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:segments>
                    <gml:LineStringSegment>
                        <gml:posList srsDimension="2">0 0 5 5</gml:posList>
                    </gml:LineStringSegment>
                    <gml:LineStringSegment>
                        <gml:posList srsDimension="2">5 5 10 0</gml:posList>
                    </gml:LineStringSegment>
                </gml:segments>
            </gml:Curve>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var curve = result.Document!.Root.Should().BeOfType<GmlCurve>().Subject;
        // 5,5 should not be duplicated
        curve.Coordinates.Should().HaveCount(3);
    }

    // ---- pointMembers (plural) in MultiPoint ----

    [Fact]
    public void ParseXmlString_WithMultiPointUsingPointMembers_ParsesCorrectly()
    {
        var xml = """
            <gml:MultiPoint xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pointMembers>
                    <gml:Point><gml:pos>1 2</gml:pos></gml:Point>
                    <gml:Point><gml:pos>3 4</gml:pos></gml:Point>
                </gml:pointMembers>
            </gml:MultiPoint>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var mp = result.Document!.Root.Should().BeOfType<GmlMultiPoint>().Subject;
        mp.Points.Should().HaveCount(2);
    }
}

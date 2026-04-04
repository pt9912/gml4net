using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

/// <summary>
/// Tests for root dispatch edge cases, standalone feature heuristics,
/// coverage dispatch, feature property edge cases, and encoding handling.
/// </summary>
public class DispatchAndFeatureEdgeCaseTests
{
    // ---- H4: FeatureCollection namespace check ----

    [Fact]
    public void ParseXmlString_WithNonGmlFeatureCollection_ReturnsUnknownRoot()
    {
        var xml = """
            <foo:FeatureCollection xmlns:foo="http://unrelated.example.com/">
            </foo:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "unknown_root");
    }

    // ---- Coverage dispatch (now parsed, not stub) ----

    [Fact]
    public void ParseXmlString_WithEmptyCoverage_ReturnsMissingDomainSet()
    {
        var xml = """
            <gml:RectifiedGridCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_domain_set");
        result.Issues.Should().NotContain(i => i.Code == "unknown_root");
    }

    [Fact]
    public void ParseXmlString_WithGmlcovCoverage_DispatchesToCoverageParser()
    {
        var xml = """
            <gmlcov:RectifiedGridCoverage xmlns:gmlcov="http://www.opengis.net/gmlcov/1.0">
            </gmlcov:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_domain_set");
    }

    // ---- M6: Standalone feature heuristic ----

    [Fact]
    public void ParseXmlString_WithHtmlRoot_ReturnsUnknownRootNotFeature()
    {
        var xml = """
            <html><body><p>Hello</p></body></html>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "unknown_root");
        result.Document.Should().BeNull();
    }

    [Fact]
    public void ParseXmlString_WithFeatureContainingGeometry_DetectedAsFeature()
    {
        // No gml:id, but has a GML geometry grandchild → detected via LooksLikeFeature
        var xml = """
            <app:Sensor xmlns:app="http://example.com/app"
                        xmlns:gml="http://www.opengis.net/gml/3.2">
                <app:location>
                    <gml:Point><gml:pos>9 48</gml:pos></gml:Point>
                </app:location>
            </app:Sensor>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlFeature>();
    }

    // ---- H5: ParseBytes encoding handling ----

    [Fact]
    public void ParseBytes_WithUtf8Bom_ParsesCorrectly()
    {
        var xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>1.0 2.0</gml:pos>
            </gml:Point>
            """;
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(xml)).ToArray();

        var result = GmlParser.ParseBytes(bytes);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlPoint>();
    }

    [Fact]
    public void ParseBytes_WithInvalidXml_ReturnsError()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("<broken<xml>");

        var result = GmlParser.ParseBytes(bytes);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "invalid_xml");
    }

    // ---- M10: Null argument guards ----

    [Fact]
    public void ParseXmlString_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GmlParser.ParseXmlString(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseStream_WithNull_ThrowsArgumentNullException()
    {
        var act = () => GmlParser.ParseStream(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ---- Feature property edge cases ----

    [Fact]
    public void ParseFeature_WithDuplicatePropertyNames_AppendsSuffix()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Item gml:id="item.1">
                        <app:tag>first</app:tag>
                        <app:tag>second</app:tag>
                        <app:tag>third</app:tag>
                    </app:Item>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties.ContainsKey("tag").Should().BeTrue();
        feature.Properties.Entries.Should().HaveCount(3);
        feature.Properties.GetValues("tag").Should().HaveCount(3);
        feature.Properties.GetValues("tag")[0].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("first");
        feature.Properties.GetValues("tag")[1].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("second");
        feature.Properties.GetValues("tag")[2].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("third");
    }

    [Fact]
    public void ParseFeature_WithRawXmlProperty_ReturnsGmlRawXmlProperty()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Item gml:id="item.1">
                        <app:data>
                            <gml:description>Some GML content</gml:description>
                        </app:data>
                    </app:Item>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties["data"].Should().BeOfType<GmlRawXmlProperty>();
    }

    [Fact]
    public void ParseFeature_WithBoundedByChild_SkipsBoundedBy()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:City gml:id="city.1">
                        <gml:boundedBy>
                            <gml:Envelope srsName="EPSG:4326">
                                <gml:lowerCorner>9 47</gml:lowerCorner>
                                <gml:upperCorner>10 48</gml:upperCorner>
                            </gml:Envelope>
                        </gml:boundedBy>
                        <app:name>Munich</app:name>
                    </app:City>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties.ContainsKey("boundedBy").Should().BeFalse();
        feature.Properties.ContainsKey("name").Should().BeTrue();
    }

    // ---- FeatureCollection with GML 2 Box in boundedBy ----

    [Fact]
    public void ParseXmlString_WithGml2BoundedByBox_ExtractsEnvelope()
    {
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml"
                                   xmlns:app="http://example.com/app">
                <gml:boundedBy>
                    <gml:Box>
                        <gml:coordinates>0,0 10,10</gml:coordinates>
                    </gml:Box>
                </gml:boundedBy>
                <gml:featureMember>
                    <app:A fid="a.1"><app:x>1</app:x></app:A>
                </gml:featureMember>
            </gml:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.BoundedBy.Should().NotBeNull();
        fc.BoundedBy!.LowerCorner.Should().Be(new GmlCoordinate(0, 0));
        fc.BoundedBy.UpperCorner.Should().Be(new GmlCoordinate(10, 10));
    }

    // ---- Unsupported GML geometry element (direct internal API) ----

    [Fact]
    public void GeometryParser_WithUnknownGmlElement_ProducesWarning()
    {
        var el = System.Xml.Linq.XElement.Parse(
            """<gml:CompositeCurve xmlns:gml="http://www.opengis.net/gml/3.2"/>""");
        var issues = new List<GmlParseIssue>();

        var result = GeometryParser.Parse(el, GmlVersion.V3_2, issues);

        result.Should().BeNull();
        issues.Should().Contain(i => i.Code == "unsupported_geometry");
    }

    [Fact]
    public void ParseXmlString_WithUnknownGmlRoot_ReturnsUnknownRoot()
    {
        var xml = """
            <gml:CompositeCurve xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:CompositeCurve>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "unknown_root");
    }

    // ---- Version detection edge cases ----

    [Fact]
    public void ParseXmlString_WithNoGmlNamespace_DefaultsToV3_2()
    {
        var xml = """
            <app:Thing xmlns:app="http://example.com/app" gml:id="t.1"
                       xmlns:gml="http://www.opengis.net/gml/3.2">
                <app:name>Test</app:name>
            </app:Thing>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.Document!.Version.Should().Be(GmlVersion.V3_2);
    }

    // ---- Empty document ----

    [Fact]
    public void ParseStream_WithEmptyXml_ReturnsError()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(""));

        var result = GmlParser.ParseStream(stream);

        result.HasErrors.Should().BeTrue();
    }

    // ---- Nested duplicate property keys ----

    [Fact]
    public void ParseFeature_WithNestedDuplicateKeys_AppendsSuffix()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Item gml:id="item.1">
                        <app:meta>
                            <app:key>a</app:key>
                            <app:key>b</app:key>
                        </app:meta>
                    </app:Item>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        var nested = feature.Properties["meta"].Should().BeOfType<GmlNestedProperty>().Subject;
        nested.Children.ContainsKey("key").Should().BeTrue();
        nested.Children.Entries.Should().HaveCount(2);
        nested.Children.GetValues("key").Should().HaveCount(2);
    }

    [Fact]
    public void ParseFeature_WithLeadingZeroInteger_PreservesStringValue()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Item gml:id="item.1">
                        <app:code>00123</app:code>
                    </app:Item>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties["code"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("00123");
    }

    [Fact]
    public void ParseXmlString_WithEmptyMemberWrapper_AddsParseIssue()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2">
                <wfs:member />
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Issues.Should().Contain(i => i.Code == "missing_feature_member");
        var featureCollection = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        featureCollection.Features.Should().BeEmpty();
    }

    // ---- GML 2 Polygon with innerBoundaryIs ----

    [Fact]
    public void ParseXmlString_WithGml2InnerBoundaryIs_ParsesInteriorRings()
    {
        var xml = """
            <gml:Polygon xmlns:gml="http://www.opengis.net/gml">
                <gml:outerBoundaryIs>
                    <gml:LinearRing>
                        <gml:coordinates>0,0 100,0 100,100 0,100 0,0</gml:coordinates>
                    </gml:LinearRing>
                </gml:outerBoundaryIs>
                <gml:innerBoundaryIs>
                    <gml:LinearRing>
                        <gml:coordinates>10,10 20,10 20,20 10,20 10,10</gml:coordinates>
                    </gml:LinearRing>
                </gml:innerBoundaryIs>
            </gml:Polygon>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var poly = result.Document!.Root.Should().BeOfType<GmlPolygon>().Subject;
        poly.Interior.Should().HaveCount(1);
    }

    // ---- Box with coord elements (GML 2) ----

    [Fact]
    public void ParseXmlString_WithBoxUsingCoordElements_ReturnsGmlBox()
    {
        var xml = """
            <gml:Box xmlns:gml="http://www.opengis.net/gml">
                <gml:coord>
                    <gml:X>0</gml:X>
                    <gml:Y>0</gml:Y>
                </gml:coord>
                <gml:coord>
                    <gml:X>10</gml:X>
                    <gml:Y>10</gml:Y>
                </gml:coord>
            </gml:Box>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var box = result.Document!.Root.Should().BeOfType<GmlBox>().Subject;
        box.LowerCorner.Should().Be(new GmlCoordinate(0, 0));
        box.UpperCorner.Should().Be(new GmlCoordinate(10, 10));
    }

    // ---- Surface with polygonPatches variant ----

    [Fact]
    public void ParseXmlString_WithSurfaceUsingPolygonPatches_ReturnsGmlSurface()
    {
        var xml = """
            <gml:Surface xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:polygonPatches>
                    <gml:PolygonPatch>
                        <gml:exterior><gml:LinearRing>
                            <gml:posList srsDimension="2">0 0 1 0 1 1 0 1 0 0</gml:posList>
                        </gml:LinearRing></gml:exterior>
                    </gml:PolygonPatch>
                </gml:polygonPatches>
            </gml:Surface>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var surface = result.Document!.Root.Should().BeOfType<GmlSurface>().Subject;
        surface.Patches.Should().HaveCount(1);
    }

    // ---- Envelope and Box now set Version ----

    [Fact]
    public void ParseXmlString_WithEnvelope_SetsVersion()
    {
        var xml = """
            <gml:Envelope xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:lowerCorner>0 0</gml:lowerCorner>
                <gml:upperCorner>1 1</gml:upperCorner>
            </gml:Envelope>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var env = result.Document!.Root.Should().BeOfType<GmlEnvelope>().Subject;
        env.Version.Should().Be(GmlVersion.V3_2);
    }

    [Fact]
    public void ParseXmlString_WithGml2Box_SetsVersion()
    {
        var xml = """
            <gml:Box xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>0,0 1,1</gml:coordinates>
            </gml:Box>
            """;

        var result = GmlParser.ParseXmlString(xml);

        var box = result.Document!.Root.Should().BeOfType<GmlBox>().Subject;
        box.Version.Should().Be(GmlVersion.V2_1_2);
    }
}

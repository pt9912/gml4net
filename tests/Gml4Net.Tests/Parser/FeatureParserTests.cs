using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

public class FeatureParserTests
{
    // ---- Simple FeatureCollection ----

    [Fact]
    public void ParseXmlString_WithGml32FeatureCollection_ReturnsFeatureCollection()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Building gml:id="building.1">
                        <app:name>Town Hall</app:name>
                        <app:height>42.5</app:height>
                        <app:geometry>
                            <gml:Point><gml:pos>10.0 20.0</gml:pos></gml:Point>
                        </app:geometry>
                    </app:Building>
                </wfs:member>
                <wfs:member>
                    <app:Building gml:id="building.2">
                        <app:name>Library</app:name>
                        <app:height>15.0</app:height>
                        <app:geometry>
                            <gml:Point><gml:pos>11.0 21.0</gml:pos></gml:Point>
                        </app:geometry>
                    </app:Building>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.Features.Should().HaveCount(2);

        fc.Features[0].Id.Should().Be("building.1");
        fc.Features[0].Properties.Should().ContainKey("name");
        fc.Features[0].Properties["name"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Town Hall");

        fc.Features[1].Id.Should().Be("building.2");
    }

    // ---- Mixed Property Types ----

    [Fact]
    public void ParseFeature_WithMixedPropertyTypes_ParsesCorrectTypes()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Sensor gml:id="sensor.1">
                        <app:label>Temperature Sensor</app:label>
                        <app:value>23.7</app:value>
                        <app:active>true</app:active>
                        <app:location>
                            <gml:Point><gml:pos>9.5 48.3</gml:pos></gml:Point>
                        </app:location>
                    </app:Sensor>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        var feature = fc.Features[0];

        // String property
        feature.Properties["label"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Temperature Sensor");

        // Numeric property
        feature.Properties["value"].Should().BeOfType<GmlNumericProperty>()
            .Which.Value.Should().Be(23.7);

        // String property (boolean text not parsed as numeric)
        feature.Properties["active"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("true");

        // Geometry property
        feature.Properties["location"].Should().BeOfType<GmlGeometryProperty>()
            .Which.Geometry.Should().BeOfType<GmlPoint>();
    }

    // ---- WFS 1.0/1.1 with gml:featureMember ----

    [Fact]
    public void ParseXmlString_WithGmlFeatureMember_ReturnsFeatureCollection()
    {
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml"
                                   xmlns:app="http://example.com/app">
                <gml:featureMember>
                    <app:Road fid="road.1">
                        <app:name>Main Street</app:name>
                        <app:centerline>
                            <gml:LineString>
                                <gml:coordinates>0,0 10,10 20,0</gml:coordinates>
                            </gml:LineString>
                        </app:centerline>
                    </app:Road>
                </gml:featureMember>
            </gml:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.Features.Should().HaveCount(1);
        fc.Features[0].Id.Should().Be("road.1");
        fc.Features[0].Properties["name"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Main Street");
        fc.Features[0].Properties["centerline"].Should().BeOfType<GmlGeometryProperty>()
            .Which.Geometry.Should().BeOfType<GmlLineString>();
    }

    // ---- GML 3.1 featureMembers (plural) ----

    [Fact]
    public void ParseXmlString_WithGmlFeatureMembers_ReturnsFeatureCollection()
    {
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <gml:featureMembers>
                    <app:Tree gml:id="tree.1">
                        <app:species>Oak</app:species>
                    </app:Tree>
                    <app:Tree gml:id="tree.2">
                        <app:species>Birch</app:species>
                    </app:Tree>
                </gml:featureMembers>
            </gml:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.Features.Should().HaveCount(2);
        fc.Features[0].Properties["species"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Oak");
        fc.Features[1].Properties["species"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Birch");
    }

    // ---- Nested Properties ----

    [Fact]
    public void ParseFeature_WithNestedProperties_ReturnsGmlNestedProperty()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Station gml:id="station.1">
                        <app:address>
                            <app:street>Main St</app:street>
                            <app:city>Berlin</app:city>
                            <app:zip>10115</app:zip>
                        </app:address>
                    </app:Station>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];

        var address = feature.Properties["address"].Should().BeOfType<GmlNestedProperty>().Subject;
        address.Children["street"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Main St");
        address.Children["city"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Berlin");
        address.Children["zip"].Should().BeOfType<GmlNumericProperty>()
            .Which.Value.Should().Be(10115);
    }

    // ---- Feature Without Geometry ----

    [Fact]
    public void ParseFeature_WithoutGeometry_ReturnsFeatureWithTextProperties()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Event gml:id="event.1">
                        <app:title>Conference</app:title>
                        <app:date>2026-04-04</app:date>
                    </app:Event>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties.Should().HaveCount(2);
        feature.Properties.Should().NotContainKey("geometry");
        feature.Properties["title"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Conference");
        feature.Properties["date"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("2026-04-04");
    }

    // ---- BoundedBy Extraction ----

    [Fact]
    public void ParseXmlString_WithBoundedBy_ExtractsBoundingBox()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <gml:boundedBy>
                    <gml:Envelope srsName="EPSG:4326">
                        <gml:lowerCorner>9.0 47.0</gml:lowerCorner>
                        <gml:upperCorner>14.0 55.0</gml:upperCorner>
                    </gml:Envelope>
                </gml:boundedBy>
                <wfs:member>
                    <app:City gml:id="city.1">
                        <app:name>Munich</app:name>
                    </app:City>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.BoundedBy.Should().NotBeNull();
        fc.BoundedBy!.LowerCorner.Should().Be(new GmlCoordinate(9.0, 47.0));
        fc.BoundedBy.UpperCorner.Should().Be(new GmlCoordinate(14.0, 55.0));
        fc.BoundedBy.SrsName.Should().Be("EPSG:4326");
    }

    // ---- Standalone Feature (no collection wrapper) ----

    [Fact]
    public void ParseXmlString_WithStandaloneFeature_ReturnsGmlFeature()
    {
        var xml = """
            <app:Building xmlns:app="http://example.com/app"
                          xmlns:gml="http://www.opengis.net/gml/3.2"
                          gml:id="building.99">
                <app:name>Standalone Building</app:name>
                <app:floors>3</app:floors>
            </app:Building>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var feature = result.Document!.Root.Should().BeOfType<GmlFeature>().Subject;
        feature.Id.Should().Be("building.99");
        feature.Properties["name"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().Be("Standalone Building");
        feature.Properties["floors"].Should().BeOfType<GmlNumericProperty>()
            .Which.Value.Should().Be(3);
    }

    // ---- Empty FeatureCollection ----

    [Fact]
    public void ParseXmlString_WithEmptyFeatureCollection_ReturnsEmptyCollection()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2">
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.Features.Should().BeEmpty();
    }

    // ---- Feature with Polygon Geometry ----

    [Fact]
    public void ParseFeature_WithPolygonGeometry_ParsesCorrectly()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Parcel gml:id="parcel.1">
                        <app:area>1500.0</app:area>
                        <app:shape>
                            <gml:Polygon>
                                <gml:exterior>
                                    <gml:LinearRing>
                                        <gml:posList srsDimension="2">0 0 10 0 10 10 0 10 0 0</gml:posList>
                                    </gml:LinearRing>
                                </gml:exterior>
                            </gml:Polygon>
                        </app:shape>
                    </app:Parcel>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties["area"].Should().BeOfType<GmlNumericProperty>()
            .Which.Value.Should().Be(1500.0);
        var geomProp = feature.Properties["shape"].Should().BeOfType<GmlGeometryProperty>().Subject;
        var polygon = geomProp.Geometry.Should().BeOfType<GmlPolygon>().Subject;
        polygon.Exterior.Coordinates.Should().HaveCount(5);
    }

    // ---- Multiple member variants mixed ----

    [Fact]
    public void ParseXmlString_WithMixedMemberVariants_CollectsAllFeatures()
    {
        var xml = """
            <gml:FeatureCollection xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:app="http://example.com/app">
                <gml:featureMember>
                    <app:A gml:id="a.1"><app:x>1</app:x></app:A>
                </gml:featureMember>
                <wfs:member>
                    <app:B gml:id="b.1"><app:x>2</app:x></app:B>
                </wfs:member>
                <gml:featureMembers>
                    <app:C gml:id="c.1"><app:x>3</app:x></app:C>
                    <app:D gml:id="d.1"><app:x>4</app:x></app:D>
                </gml:featureMembers>
            </gml:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var fc = result.Document!.Root.Should().BeOfType<GmlFeatureCollection>().Subject;
        fc.Features.Should().HaveCount(4);
        fc.Features[0].Id.Should().Be("a.1");
        fc.Features[1].Id.Should().Be("b.1");
        fc.Features[2].Id.Should().Be("c.1");
        fc.Features[3].Id.Should().Be("d.1");
    }

    // ---- Feature with empty property ----

    [Fact]
    public void ParseFeature_WithEmptyProperty_ReturnsEmptyString()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:Item gml:id="item.1">
                        <app:note/>
                        <app:label>Test</app:label>
                    </app:Item>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var feature = (result.Document!.Root as GmlFeatureCollection)!.Features[0];
        feature.Properties["note"].Should().BeOfType<GmlStringProperty>()
            .Which.Value.Should().BeEmpty();
    }
}

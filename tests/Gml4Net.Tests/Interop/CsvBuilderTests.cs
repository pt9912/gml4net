using FluentAssertions;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Interop;

public class CsvBuilderTests
{
    [Fact]
    public void FeatureCollection_WithFeaturesAndGeometry_ReturnsCsv()
    {
        var fc = new GmlFeatureCollection
        {
            Features =
            [
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "name", Value = new GmlStringProperty { Value = "Munich" } },
                        new GmlPropertyEntry { Name = "pop", Value = new GmlNumericProperty { Value = 1500000 } },
                        new GmlPropertyEntry
                        {
                            Name = "geom",
                            Value = new GmlGeometryProperty
                            {
                                Geometry = new GmlPoint { Coordinate = new(11.5, 48.1) }
                            }
                        }
                    ])
                },
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "name", Value = new GmlStringProperty { Value = "Berlin" } },
                        new GmlPropertyEntry { Name = "pop", Value = new GmlNumericProperty { Value = 3700000 } },
                        new GmlPropertyEntry
                        {
                            Name = "geom",
                            Value = new GmlGeometryProperty
                            {
                                Geometry = new GmlPoint { Coordinate = new(13.4, 52.5) }
                            }
                        }
                    ])
                }
            ]
        };

        var csv = CsvBuilder.FeatureCollection(fc);

        csv.Should().Contain("geometry,name,pop");
        csv.Should().Contain("POINT (11.5 48.1)");
        csv.Should().Contain("Munich");
        csv.Should().Contain("1500000");
        csv.Should().Contain("POINT (13.4 52.5)");
    }

    [Fact]
    public void FeatureCollection_WithoutGeometry_OmitsGeometryColumn()
    {
        var fc = new GmlFeatureCollection
        {
            Features =
            [
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "name", Value = new GmlStringProperty { Value = "Test" } }
                    ])
                }
            ]
        };

        var csv = CsvBuilder.FeatureCollection(fc);

        csv.Should().StartWith("name");
        csv.Should().NotContain("geometry");
    }

    [Fact]
    public void FeatureCollection_Empty_ReturnsEmpty()
    {
        var fc = new GmlFeatureCollection();
        CsvBuilder.FeatureCollection(fc).Should().BeEmpty();
    }

    [Fact]
    public void FeatureCollection_WithCommaInValue_EscapesCorrectly()
    {
        var fc = new GmlFeatureCollection
        {
            Features =
            [
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "addr", Value = new GmlStringProperty { Value = "Street, 5" } }
                    ])
                }
            ]
        };

        var csv = CsvBuilder.FeatureCollection(fc);

        csv.Should().Contain("\"Street, 5\"");
    }

    [Fact]
    public void FeatureCollection_WithQuoteInValue_EscapesCorrectly()
    {
        var fc = new GmlFeatureCollection
        {
            Features =
            [
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "note", Value = new GmlStringProperty { Value = "He said \"hello\"" } }
                    ])
                }
            ]
        };

        var csv = CsvBuilder.FeatureCollection(fc);

        csv.Should().Contain("\"He said \"\"hello\"\"\"");
    }

    [Fact]
    public void FeatureCollection_WithCustomSeparator_UsesSeparator()
    {
        var fc = new GmlFeatureCollection
        {
            Features =
            [
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "a", Value = new GmlStringProperty { Value = "1" } },
                        new GmlPropertyEntry { Name = "b", Value = new GmlStringProperty { Value = "2" } }
                    ])
                }
            ]
        };

        var csv = CsvBuilder.FeatureCollection(fc, ';');

        csv.Should().Contain("a;b");
        csv.Should().Contain("1;2");
    }

    [Fact]
    public void FeatureCollection_WithMissingProperty_OutputsEmptyCell()
    {
        var fc = new GmlFeatureCollection
        {
            Features =
            [
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "a", Value = new GmlStringProperty { Value = "1" } },
                        new GmlPropertyEntry { Name = "b", Value = new GmlStringProperty { Value = "2" } }
                    ])
                },
                new GmlFeature
                {
                    Properties = new GmlPropertyBag(
                    [
                        new GmlPropertyEntry { Name = "a", Value = new GmlStringProperty { Value = "3" } }
                    ])
                }
            ]
        };

        var csv = CsvBuilder.FeatureCollection(fc);
        var lines = csv.Trim().Split('\n');

        lines[0].Trim().Should().Be("a,b");
        lines[2].Trim().Should().Be("3,"); // missing b
    }

    // ---- Roundtrip ----

    [Fact]
    public void Roundtrip_ParseGmlThenConvertToCsv()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member>
                    <app:City gml:id="city.1">
                        <app:name>Munich</app:name>
                        <app:location>
                            <gml:Point><gml:pos>11.5 48.1</gml:pos></gml:Point>
                        </app:location>
                    </app:City>
                </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = Gml4Net.Parser.GmlParser.ParseXmlString(xml);
        var csv = CsvBuilder.FeatureCollection((GmlFeatureCollection)result.Document!.Root);

        csv.Should().Contain("geometry,name");
        csv.Should().Contain("POINT (11.5 48.1)");
        csv.Should().Contain("Munich");
    }
}

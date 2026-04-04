using System.Xml.Linq;
using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

public class VersionDetectionTests
{
    [Fact]
    public void DetectVersion_Gml32Namespace_ReturnsV3_2()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:pos>10.0 20.0</gml:pos>
            </gml:Point>
            """;
        var doc = XDocument.Parse(xml);

        XmlHelpers.DetectVersion(doc).Should().Be(GmlVersion.V3_2);
    }

    [Fact]
    public void DetectVersion_Gml33Namespace_ReturnsV3_3()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml/3.3">
                <gml:pos>10.0 20.0</gml:pos>
            </gml:Point>
            """;
        var doc = XDocument.Parse(xml);

        XmlHelpers.DetectVersion(doc).Should().Be(GmlVersion.V3_3);
    }

    [Fact]
    public void DetectVersion_GmlNamespaceWithCoordinates_ReturnsV2_1_2()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>10.0,20.0</gml:coordinates>
            </gml:Point>
            """;
        var doc = XDocument.Parse(xml);

        XmlHelpers.DetectVersion(doc).Should().Be(GmlVersion.V2_1_2);
    }

    [Fact]
    public void DetectVersion_GmlNamespaceWithBox_ReturnsV2_1_2()
    {
        var xml = """
            <gml:Box xmlns:gml="http://www.opengis.net/gml">
                <gml:coordinates>0,0 10,10</gml:coordinates>
            </gml:Box>
            """;
        var doc = XDocument.Parse(xml);

        XmlHelpers.DetectVersion(doc).Should().Be(GmlVersion.V2_1_2);
    }

    [Fact]
    public void DetectVersion_GmlNamespaceWithPos_ReturnsV3_1()
    {
        var xml = """
            <gml:Point xmlns:gml="http://www.opengis.net/gml">
                <gml:pos>10.0 20.0</gml:pos>
            </gml:Point>
            """;
        var doc = XDocument.Parse(xml);

        XmlHelpers.DetectVersion(doc).Should().Be(GmlVersion.V3_1);
    }
}

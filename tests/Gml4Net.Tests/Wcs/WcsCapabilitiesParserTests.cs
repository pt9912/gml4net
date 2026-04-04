using FluentAssertions;
using Gml4Net.Wcs;
using Xunit;

namespace Gml4Net.Tests.Wcs;

public class WcsCapabilitiesParserTests
{
    [Fact]
    public void Parse_Wcs20Capabilities_ParsesAllSections()
    {
        var xml = """
            <wcs:Capabilities xmlns:wcs="http://www.opengis.net/wcs/2.0"
                              xmlns:ows="http://www.opengis.net/ows/1.1"
                              xmlns:xlink="http://www.w3.org/1999/xlink"
                              version="2.0.1">
                <ows:ServiceIdentification>
                    <ows:Title>Test WCS</ows:Title>
                    <ows:Abstract>A test service</ows:Abstract>
                    <ows:Keywords>
                        <ows:Keyword>elevation</ows:Keyword>
                        <ows:Keyword>DEM</ows:Keyword>
                    </ows:Keywords>
                </ows:ServiceIdentification>
                <ows:OperationsMetadata>
                    <ows:Operation name="GetCoverage">
                        <ows:DCP>
                            <ows:HTTP>
                                <ows:Get xlink:href="https://example.com/wcs?"/>
                                <ows:Post xlink:href="https://example.com/wcs"/>
                            </ows:HTTP>
                        </ows:DCP>
                    </ows:Operation>
                    <ows:Operation name="GetCapabilities">
                        <ows:DCP>
                            <ows:HTTP>
                                <ows:Get xlink:href="https://example.com/wcs?"/>
                            </ows:HTTP>
                        </ows:DCP>
                    </ows:Operation>
                </ows:OperationsMetadata>
                <wcs:ServiceMetadata>
                    <wcs:formatSupported>image/tiff</wcs:formatSupported>
                    <wcs:formatSupported>application/gml+xml</wcs:formatSupported>
                </wcs:ServiceMetadata>
                <wcs:Contents>
                    <wcs:CoverageSummary>
                        <wcs:CoverageId>dem_10m</wcs:CoverageId>
                        <wcs:CoverageSubtype>RectifiedGridCoverage</wcs:CoverageSubtype>
                        <ows:WGS84BoundingBox>
                            <ows:LowerCorner>5.0 47.0</ows:LowerCorner>
                            <ows:UpperCorner>15.0 55.0</ows:UpperCorner>
                        </ows:WGS84BoundingBox>
                    </wcs:CoverageSummary>
                    <wcs:CoverageSummary>
                        <wcs:CoverageId>ortho_rgb</wcs:CoverageId>
                    </wcs:CoverageSummary>
                </wcs:Contents>
            </wcs:Capabilities>
            """;

        var caps = WcsCapabilitiesParser.Parse(xml);

        caps.Version.Should().Be("2.0.1");

        // ServiceIdentification
        caps.ServiceIdentification.Should().NotBeNull();
        caps.ServiceIdentification!.Title.Should().Be("Test WCS");
        caps.ServiceIdentification.Abstract.Should().Be("A test service");
        caps.ServiceIdentification.Keywords.Should().Equal("elevation", "DEM");

        // Operations
        caps.Operations.Should().HaveCount(2);
        caps.Operations[0].Name.Should().Be("GetCoverage");
        caps.Operations[0].GetUrl.Should().Be("https://example.com/wcs?");
        caps.Operations[0].PostUrl.Should().Be("https://example.com/wcs");
        caps.Operations[1].Name.Should().Be("GetCapabilities");

        // Formats
        caps.Formats.Should().Equal("image/tiff", "application/gml+xml");

        // Coverages
        caps.Coverages.Should().HaveCount(2);
        caps.Coverages[0].CoverageId.Should().Be("dem_10m");
        caps.Coverages[0].Subtype.Should().Be("RectifiedGridCoverage");
        caps.Coverages[0].Bbox.Should().Equal(5.0, 47.0, 15.0, 55.0);
        caps.Coverages[1].CoverageId.Should().Be("ortho_rgb");
        caps.Coverages[1].Bbox.Should().BeNull();
    }

    [Fact]
    public void Parse_MinimalCapabilities_ReturnsDefaults()
    {
        var xml = """
            <wcs:Capabilities xmlns:wcs="http://www.opengis.net/wcs/2.0" version="2.0.0">
            </wcs:Capabilities>
            """;

        var caps = WcsCapabilitiesParser.Parse(xml);

        caps.Version.Should().Be("2.0.0");
        caps.ServiceIdentification.Should().BeNull();
        caps.Operations.Should().BeEmpty();
        caps.Coverages.Should().BeEmpty();
        caps.Formats.Should().BeEmpty();
        caps.Crs.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WithCrsSupported_ParsesCrs()
    {
        var xml = """
            <wcs:Capabilities xmlns:wcs="http://www.opengis.net/wcs/2.0"
                              xmlns:crs="http://www.opengis.net/wcs/crs/1.0"
                              version="2.0.1">
                <wcs:ServiceMetadata>
                    <crs:crsSupported>EPSG:4326</crs:crsSupported>
                    <crs:crsSupported>EPSG:3857</crs:crsSupported>
                </wcs:ServiceMetadata>
            </wcs:Capabilities>
            """;

        var caps = WcsCapabilitiesParser.Parse(xml);

        caps.Crs.Should().Equal("EPSG:4326", "EPSG:3857");
    }
}

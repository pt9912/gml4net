using FluentAssertions;
using Gml4Net.Wcs;
using Xunit;

namespace Gml4Net.Tests.Wcs;

public class WcsRequestBuilderTests
{
    [Fact]
    public void BuildGetCoverageUrl_V2_0_1_ReturnsCorrectUrl()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs");
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions
        {
            CoverageId = "dem_10m",
            Format = "image/tiff"
        });

        url.Should().Contain("service=WCS");
        url.Should().Contain("request=GetCoverage");
        url.Should().Contain("version=2.0.1");
        url.Should().Contain("CoverageId=dem_10m");
        url.Should().Contain("format=image%2Ftiff");
    }

    [Fact]
    public void BuildGetCoverageUrl_V1_0_0_UsesCoverageParam()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs", WcsVersion.V1_0_0);
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions { CoverageId = "test" });

        url.Should().Contain("coverage=test");
        url.Should().Contain("version=1.0.0");
    }

    [Fact]
    public void BuildGetCoverageUrl_V1_1_0_UsesIdentifierParam()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs", WcsVersion.V1_1_0);
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions { CoverageId = "test" });

        url.Should().Contain("identifier=test");
    }

    [Fact]
    public void BuildGetCoverageUrl_V1_1_0_WithSubsets_ThrowsNotSupportedException()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs", WcsVersion.V1_1_0);

        var act = () => builder.BuildGetCoverageUrl(new WcsGetCoverageOptions
        {
            CoverageId = "test",
            Subsets = [new WcsSubset { Axis = "Long", Min = "10", Max = "20" }]
        });

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void BuildGetCoverageUrl_WithSubsets_IncludesSubsetParams()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs");
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions
        {
            CoverageId = "dem",
            Subsets =
            [
                new WcsSubset { Axis = "Long", Min = "10", Max = "20" },
                new WcsSubset { Axis = "Lat", Min = "47", Max = "55" }
            ]
        });

        url.Should().Contain("subset=Long%2810%2C20%29");
        url.Should().Contain("subset=Lat%2847%2C55%29");
    }

    [Fact]
    public void BuildGetCoverageUrl_WithPointSubset_FormatsCorrectly()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs");
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions
        {
            CoverageId = "dem",
            Subsets = [new WcsSubset { Axis = "time", Value = "2020-01-01" }]
        });

        url.Should().Contain("subset=time%282020-01-01%29");
    }

    [Fact]
    public void BuildGetCoverageUrl_WithAllOptions_IncludesAll()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs");
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions
        {
            CoverageId = "dem",
            Format = "image/tiff",
            OutputCrs = "EPSG:4326",
            RangeSubset = ["band1", "band2"],
            Interpolation = "nearest"
        });

        url.Should().Contain("outputCrs=EPSG%3A4326");
        url.Should().Contain("rangesubset=band1%2Cband2");
        url.Should().Contain("interpolation=nearest");
    }

    [Fact]
    public void BuildGetCoverageUrl_BaseUrlWithQueryParam_UsesAmpersand()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs?key=abc");
        var url = builder.BuildGetCoverageUrl(new WcsGetCoverageOptions { CoverageId = "dem" });

        url.Should().StartWith("https://example.com/wcs?key=abc&service=WCS");
    }

    // ---- XML ----

    [Fact]
    public void BuildGetCoverageXml_ReturnsValidXml()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs", WcsVersion.V2_0_1);
        var xml = builder.BuildGetCoverageXml(new WcsGetCoverageOptions
        {
            CoverageId = "dem_10m",
            Format = "image/tiff",
            Subsets =
            [
                new WcsSubset { Axis = "Long", Min = "10", Max = "20" },
                new WcsSubset { Axis = "time", Value = "2020-01-01" }
            ]
        });

        xml.Should().Contain("GetCoverage");
        xml.Should().Contain("dem_10m");
        xml.Should().Contain("DimensionTrim");
        xml.Should().Contain("DimensionSlice");
        xml.Should().Contain("Long");
        xml.Should().Contain("TrimLow");
        xml.Should().Contain("TrimHigh");
        xml.Should().Contain("SlicePoint");
        xml.Should().Contain("image/tiff");
    }

    [Fact]
    public void BuildGetCoverageXml_WithPointSubset_UsesDimensionSlice()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs", WcsVersion.V2_0_1);

        var xml = builder.BuildGetCoverageXml(new WcsGetCoverageOptions
        {
            CoverageId = "dem",
            Subsets = [new WcsSubset { Axis = "time", Value = "2020-01-01" }]
        });

        xml.Should().Contain("DimensionSlice");
        xml.Should().NotContain("DimensionTrim><wcs:Dimension>time");
    }

    [Fact]
    public void BuildGetCoverageXml_V1_0_0_ThrowsNotSupportedException()
    {
        var builder = new WcsRequestBuilder("https://example.com/wcs", WcsVersion.V1_0_0);

        var act = () => builder.BuildGetCoverageXml(new WcsGetCoverageOptions { CoverageId = "dem" });

        act.Should().Throw<NotSupportedException>();
    }
}

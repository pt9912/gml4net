using FluentAssertions;
using Gml4Net.Ows;
using Xunit;

namespace Gml4Net.Tests.Ows;

public class OwsExceptionTests
{
    [Fact]
    public void IsOwsExceptionReport_WithValidReport_ReturnsTrue()
    {
        var xml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
                <ows:Exception exceptionCode="NoSuchCoverage">
                    <ows:ExceptionText>Coverage 'xyz' not found</ows:ExceptionText>
                </ows:Exception>
            </ows:ExceptionReport>
            """;

        OwsExceptionParser.IsOwsExceptionReport(xml).Should().BeTrue();
    }

    [Fact]
    public void IsOwsExceptionReport_WithNonOwsXml_ReturnsFalse()
    {
        var xml = """<gml:Point xmlns:gml="http://www.opengis.net/gml/3.2"><gml:pos>1 2</gml:pos></gml:Point>""";
        OwsExceptionParser.IsOwsExceptionReport(xml).Should().BeFalse();
    }

    [Fact]
    public void IsOwsExceptionReport_WithInvalidXml_ReturnsFalse()
    {
        OwsExceptionParser.IsOwsExceptionReport("<broken<").Should().BeFalse();
    }

    [Fact]
    public void Parse_WithSingleException_ReturnsReport()
    {
        var xml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
                <ows:Exception exceptionCode="NoSuchCoverage" locator="CoverageId">
                    <ows:ExceptionText>Coverage 'test' not found</ows:ExceptionText>
                </ows:Exception>
            </ows:ExceptionReport>
            """;

        var report = OwsExceptionParser.Parse(xml);

        report.Should().NotBeNull();
        report!.Version.Should().Be("2.0.0");
        report.Exceptions.Should().HaveCount(1);
        report.Exceptions[0].ExceptionCode.Should().Be("NoSuchCoverage");
        report.Exceptions[0].Locator.Should().Be("CoverageId");
        report.Exceptions[0].ExceptionTexts.Should().ContainSingle("Coverage 'test' not found");
    }

    [Fact]
    public void Parse_WithMultipleExceptions_ReturnsAll()
    {
        var xml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="1.1.0">
                <ows:Exception exceptionCode="InvalidParameterValue">
                    <ows:ExceptionText>Invalid CRS</ows:ExceptionText>
                </ows:Exception>
                <ows:Exception exceptionCode="MissingParameterValue">
                    <ows:ExceptionText>Missing format</ows:ExceptionText>
                    <ows:ExceptionText>Missing coverage</ows:ExceptionText>
                </ows:Exception>
            </ows:ExceptionReport>
            """;

        var report = OwsExceptionParser.Parse(xml);

        report!.Exceptions.Should().HaveCount(2);
        report.Exceptions[1].ExceptionTexts.Should().HaveCount(2);
        report.AllMessages.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_WithNonOwsXml_ReturnsNull()
    {
        var report = OwsExceptionParser.Parse("<root/>");
        report.Should().BeNull();
    }

    [Fact]
    public void Parse_WithInvalidXml_ReturnsNull()
    {
        var report = OwsExceptionParser.Parse("<broken<");
        report.Should().BeNull();
    }

    [Fact]
    public void Parse_WithExceptionWithoutTexts_ReturnsEmptyTexts()
    {
        var xml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
                <ows:Exception exceptionCode="ServerError"/>
            </ows:ExceptionReport>
            """;

        var report = OwsExceptionParser.Parse(xml);

        report!.Exceptions[0].ExceptionTexts.Should().BeEmpty();
        report.Exceptions[0].Locator.Should().BeNull();
    }

    [Fact]
    public void Parse_WithOws20Namespace_ReturnsReport()
    {
        var xml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/2.0" version="2.0.1">
                <ows:Exception exceptionCode="NoApplicableCode">
                    <ows:ExceptionText>Failure</ows:ExceptionText>
                </ows:Exception>
            </ows:ExceptionReport>
            """;

        var report = OwsExceptionParser.Parse(xml);

        report.Should().NotBeNull();
        report!.Exceptions.Should().ContainSingle();
        report.Exceptions[0].ExceptionCode.Should().Be("NoApplicableCode");
    }
}

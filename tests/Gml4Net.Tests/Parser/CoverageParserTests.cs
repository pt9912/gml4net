using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Parser;

public class CoverageParserTests
{
    // ---- RectifiedGridCoverage ----

    [Fact]
    public void ParseXmlString_WithRectifiedGridCoverage_ParsesCorrectly()
    {
        var xml = """
            <gml:RectifiedGridCoverage xmlns:gml="http://www.opengis.net/gml/3.2"
                                       xmlns:swe="http://www.opengis.net/swe/2.0"
                                       gml:id="cov1">
                <gml:boundedBy>
                    <gml:Envelope srsName="EPSG:4326">
                        <gml:lowerCorner>0 0</gml:lowerCorner>
                        <gml:upperCorner>10 10</gml:upperCorner>
                    </gml:Envelope>
                </gml:boundedBy>
                <gml:domainSet>
                    <gml:RectifiedGrid dimension="2" srsName="EPSG:4326" axisLabels="x y">
                        <gml:limits>
                            <gml:GridEnvelope>
                                <gml:low>0 0</gml:low>
                                <gml:high>99 99</gml:high>
                            </gml:GridEnvelope>
                        </gml:limits>
                        <gml:origin>
                            <gml:Point><gml:pos>0 0</gml:pos></gml:Point>
                        </gml:origin>
                        <gml:offsetVector>0.1 0</gml:offsetVector>
                        <gml:offsetVector>0 0.1</gml:offsetVector>
                    </gml:RectifiedGrid>
                </gml:domainSet>
                <gml:rangeSet>
                    <gml:DataBlock>
                        <gml:tupleList>1 2 3 4</gml:tupleList>
                    </gml:DataBlock>
                </gml:rangeSet>
                <gml:rangeType>
                    <swe:DataRecord>
                        <swe:field name="band1">
                            <swe:Quantity>
                                <swe:description>Red</swe:description>
                                <swe:uom code="dn"/>
                            </swe:Quantity>
                        </swe:field>
                    </swe:DataRecord>
                </gml:rangeType>
            </gml:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var cov = result.Document!.Root.Should().BeOfType<GmlRectifiedGridCoverage>().Subject;
        cov.Id.Should().Be("cov1");
        cov.BoundedBy.Should().NotBeNull();
        cov.DomainSet.Dimension.Should().Be(2);
        cov.DomainSet.SrsName.Should().Be("EPSG:4326");
        cov.DomainSet.AxisLabels.Should().Equal("x", "y");
        cov.DomainSet.Limits.Low.Should().Equal(0, 0);
        cov.DomainSet.Limits.High.Should().Equal(99, 99);
        cov.DomainSet.Origin.X.Should().Be(0);
        cov.DomainSet.OffsetVectors.Should().HaveCount(2);
        cov.DomainSet.OffsetVectors[0].Should().Equal(0.1, 0);
        cov.DomainSet.OffsetVectors[1].Should().Equal(0, 0.1);
        cov.RangeSet!.DataBlock.Should().Be("1 2 3 4");
        cov.RangeType!.Fields.Should().HaveCount(1);
        cov.RangeType.Fields[0].Name.Should().Be("band1");
        cov.RangeType.Fields[0].Description.Should().Be("Red");
        cov.RangeType.Fields[0].Uom.Should().Be("dn");
    }

    // ---- GridCoverage ----

    [Fact]
    public void ParseXmlString_WithGridCoverage_ParsesCorrectly()
    {
        var xml = """
            <gml:GridCoverage xmlns:gml="http://www.opengis.net/gml/3.2" gml:id="gc1">
                <gml:domainSet>
                    <gml:Grid dimension="2">
                        <gml:limits>
                            <gml:GridEnvelope>
                                <gml:low>0 0</gml:low>
                                <gml:high>49 49</gml:high>
                            </gml:GridEnvelope>
                        </gml:limits>
                    </gml:Grid>
                </gml:domainSet>
            </gml:GridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var cov = result.Document!.Root.Should().BeOfType<GmlGridCoverage>().Subject;
        cov.Id.Should().Be("gc1");
        cov.DomainSet.Dimension.Should().Be(2);
        cov.DomainSet.Limits.High.Should().Equal(49, 49);
    }

    // ---- ReferenceableGridCoverage ----

    [Fact]
    public void ParseXmlString_WithReferenceableGridCoverage_ParsesCorrectly()
    {
        var xml = """
            <gml:ReferenceableGridCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:domainSet>
                    <gml:Grid dimension="2">
                        <gml:limits>
                            <gml:GridEnvelope>
                                <gml:low>0 0</gml:low>
                                <gml:high>9 9</gml:high>
                            </gml:GridEnvelope>
                        </gml:limits>
                    </gml:Grid>
                </gml:domainSet>
            </gml:ReferenceableGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlReferenceableGridCoverage>();
    }

    // ---- MultiPointCoverage ----

    [Fact]
    public void ParseXmlString_WithMultiPointCoverage_ParsesCorrectly()
    {
        var xml = """
            <gml:MultiPointCoverage xmlns:gml="http://www.opengis.net/gml/3.2" gml:id="mpc1">
                <gml:domainSet>
                    <gml:MultiPoint>
                        <gml:pointMember>
                            <gml:Point><gml:pos>1 2</gml:pos></gml:Point>
                        </gml:pointMember>
                        <gml:pointMember>
                            <gml:Point><gml:pos>3 4</gml:pos></gml:Point>
                        </gml:pointMember>
                    </gml:MultiPoint>
                </gml:domainSet>
                <gml:rangeSet>
                    <gml:File>
                        <gml:fileName>data.tif</gml:fileName>
                    </gml:File>
                </gml:rangeSet>
            </gml:MultiPointCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var cov = result.Document!.Root.Should().BeOfType<GmlMultiPointCoverage>().Subject;
        cov.Id.Should().Be("mpc1");
        cov.DomainPoints.Should().HaveCount(2);
        cov.RangeSet!.FileReference.Should().Be("data.tif");
    }

    // ---- GMLCOV namespace ----

    [Fact]
    public void ParseXmlString_WithGmlcovNamespace_ParsesCorrectly()
    {
        var xml = """
            <gmlcov:RectifiedGridCoverage xmlns:gmlcov="http://www.opengis.net/gmlcov/1.0"
                                          xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:domainSet>
                    <gml:RectifiedGrid dimension="2">
                        <gml:limits>
                            <gml:GridEnvelope>
                                <gml:low>0 0</gml:low>
                                <gml:high>9 9</gml:high>
                            </gml:GridEnvelope>
                        </gml:limits>
                        <gml:origin>
                            <gml:Point><gml:pos>0 0</gml:pos></gml:Point>
                        </gml:origin>
                        <gml:offsetVector>1 0</gml:offsetVector>
                        <gml:offsetVector>0 1</gml:offsetVector>
                    </gml:RectifiedGrid>
                </gml:domainSet>
            </gmlcov:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlRectifiedGridCoverage>();
    }

    // ---- Edge cases ----

    [Fact]
    public void ParseXmlString_WithCoverageMissingDomainSet_ReturnsError()
    {
        var xml = """
            <gml:RectifiedGridCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_domain_set");
    }

    [Fact]
    public void ParseXmlString_WithGridMissingLimits_ReturnsError()
    {
        var xml = """
            <gml:GridCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:domainSet>
                    <gml:Grid dimension="2">
                    </gml:Grid>
                </gml:domainSet>
            </gml:GridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_limits");
    }

    [Fact]
    public void ParseXmlString_WithMultiPointCoverageMissingDomain_StillParses()
    {
        var xml = """
            <gml:MultiPointCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
            </gml:MultiPointCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var cov = result.Document!.Root.Should().BeOfType<GmlMultiPointCoverage>().Subject;
        cov.DomainPoints.Should().BeNull();
    }

    [Fact]
    public void ParseXmlString_WithRectifiedGridMissingRectifiedGrid_ReturnsError()
    {
        var xml = """
            <gml:RectifiedGridCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:domainSet>
                </gml:domainSet>
            </gml:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "missing_rectified_grid");
    }

    [Fact]
    public void ParseXmlString_WithOriginPosDirect_ParsesOrigin()
    {
        // Some docs put pos directly under origin (no Point wrapper)
        var xml = """
            <gml:RectifiedGridCoverage xmlns:gml="http://www.opengis.net/gml/3.2">
                <gml:domainSet>
                    <gml:RectifiedGrid dimension="2">
                        <gml:limits>
                            <gml:GridEnvelope>
                                <gml:low>0 0</gml:low>
                                <gml:high>9 9</gml:high>
                            </gml:GridEnvelope>
                        </gml:limits>
                        <gml:origin>
                            <gml:pos>5 10</gml:pos>
                        </gml:origin>
                        <gml:offsetVector>1 0</gml:offsetVector>
                        <gml:offsetVector>0 1</gml:offsetVector>
                    </gml:RectifiedGrid>
                </gml:domainSet>
            </gml:RectifiedGridCoverage>
            """;

        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse();
        var cov = result.Document!.Root.Should().BeOfType<GmlRectifiedGridCoverage>().Subject;
        cov.DomainSet.Origin.X.Should().Be(5);
        cov.DomainSet.Origin.Y.Should().Be(10);
    }
}

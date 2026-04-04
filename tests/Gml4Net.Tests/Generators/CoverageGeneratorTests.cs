using FluentAssertions;
using Gml4Net.Generators;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Geometry;
using Gml4Net.Parser;
using Xunit;

namespace Gml4Net.Tests.Generators;

public class CoverageGeneratorTests
{
    [Fact]
    public void Generate_RectifiedGridCoverage_ProducesValidXml()
    {
        var coverage = CreateSampleRectifiedGridCoverage();

        var xml = CoverageGenerator.Generate(coverage);

        xml.Should().Contain("RectifiedGridCoverage");
        xml.Should().Contain("RectifiedGrid");
        xml.Should().Contain("offsetVector");
        xml.Should().Contain("tupleList");
        xml.Should().Contain("band1");
    }

    [Fact]
    public void Generate_RectifiedGridCoverage_RoundtripParsesBack()
    {
        var original = CreateSampleRectifiedGridCoverage();

        var xml = CoverageGenerator.Generate(original);
        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse(because: string.Join("; ", result.Issues.Select(i => i.Message)));
        var parsed = result.Document!.Root.Should().BeOfType<GmlRectifiedGridCoverage>().Subject;
        parsed.DomainSet.Dimension.Should().Be(2);
        parsed.DomainSet.Origin.X.Should().Be(0);
        parsed.DomainSet.OffsetVectors.Should().HaveCount(2);
        parsed.RangeSet!.DataBlock.Should().Be("1 2 3");
        parsed.RangeType!.Fields[0].Name.Should().Be("band1");
    }

    [Fact]
    public void Generate_GridCoverage_RoundtripParsesBack()
    {
        var coverage = new GmlGridCoverage
        {
            Id = "gc1",
            DomainSet = new GmlGrid
            {
                Dimension = 2,
                Limits = new GmlGridEnvelope { Low = [0, 0], High = [49, 49] },
                AxisLabels = ["x", "y"]
            }
        };

        var xml = CoverageGenerator.Generate(coverage);
        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse(because: string.Join("; ", result.Issues.Select(i => i.Message)));
        result.Document!.Root.Should().BeOfType<GmlGridCoverage>();
    }

    [Fact]
    public void Generate_ReferenceableGridCoverage_RoundtripParsesBack()
    {
        var coverage = new GmlReferenceableGridCoverage
        {
            DomainSet = new GmlGrid
            {
                Dimension = 2,
                Limits = new GmlGridEnvelope { Low = [0, 0], High = [9, 9] }
            }
        };

        var xml = CoverageGenerator.Generate(coverage);
        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse(because: string.Join("; ", result.Issues.Select(i => i.Message)));
        result.Document!.Root.Should().BeOfType<GmlReferenceableGridCoverage>();
    }

    [Fact]
    public void Generate_ReferenceableGridCoverage_UsesReferenceableGridElement()
    {
        var coverage = new GmlReferenceableGridCoverage
        {
            DomainSet = new GmlGrid
            {
                Dimension = 2,
                Limits = new GmlGridEnvelope { Low = [0, 0], High = [9, 9] }
            }
        };

        var xml = CoverageGenerator.Generate(coverage);

        xml.Should().Contain("ReferenceableGrid");
        xml.Should().NotContain("<gml:Grid ");
        xml.Should().NotContain("<gml:Grid>");
    }

    [Fact]
    public void Generate_MultiPointCoverage_RoundtripParsesBack()
    {
        var coverage = new GmlMultiPointCoverage
        {
            Id = "mpc1",
            DomainPoints =
            [
                new GmlPoint { Coordinate = new GmlCoordinate(1, 2) },
                new GmlPoint { Coordinate = new GmlCoordinate(3, 4) }
            ],
            RangeSet = new GmlRangeSet { FileReference = "data.tif" }
        };

        var xml = CoverageGenerator.Generate(coverage);
        var result = GmlParser.ParseXmlString(xml);

        result.HasErrors.Should().BeFalse(because: string.Join("; ", result.Issues.Select(i => i.Message)));
        var parsed = result.Document!.Root.Should().BeOfType<GmlMultiPointCoverage>().Subject;
        parsed.DomainPoints.Should().HaveCount(2);
    }

    [Fact]
    public void Generate_WithPrettyPrintFalse_ProducesCompactXml()
    {
        var coverage = CreateSampleRectifiedGridCoverage();

        var xml = CoverageGenerator.Generate(coverage, prettyPrint: false);

        xml.Should().NotContain("\n  ");
    }

    [Fact]
    public void Generate_WithBoundedBy_IncludesBoundedBy()
    {
        var coverage = CreateSampleRectifiedGridCoverage();

        var xml = CoverageGenerator.Generate(coverage);

        xml.Should().Contain("boundedBy");
        xml.Should().Contain("EPSG:4326");
    }

    private static GmlRectifiedGridCoverage CreateSampleRectifiedGridCoverage() => new()
    {
        Id = "test-cov",
        BoundedBy = new GmlEnvelope
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(10, 10),
            SrsName = "EPSG:4326"
        },
        DomainSet = new GmlRectifiedGrid
        {
            Dimension = 2,
            Limits = new GmlGridEnvelope { Low = [0, 0], High = [99, 99] },
            SrsName = "EPSG:4326",
            Origin = new GmlCoordinate(0, 0),
            OffsetVectors = [new double[] { 0.1, 0 }, new double[] { 0, 0.1 }],
            AxisLabels = ["x", "y"]
        },
        RangeSet = new GmlRangeSet { DataBlock = "1 2 3" },
        RangeType = new GmlRangeType
        {
            Fields = [new GmlRangeField { Name = "band1", Description = "Red", Uom = "dn" }]
        }
    };
}

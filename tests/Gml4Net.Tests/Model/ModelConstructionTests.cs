using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.Tests.Model;

/// <summary>
/// Tests exercising model construction paths for coverage of init-only properties,
/// default values, and types not yet exercised by parser tests.
/// </summary>
public class ModelConstructionTests
{
    // ---- Coverage model types ----

    [Fact]
    public void GmlRectifiedGridCoverage_CanBeConstructed()
    {
        var grid = new GmlRectifiedGrid
        {
            Dimension = 2,
            Limits = new GmlGridEnvelope { Low = [0, 0], High = [99, 99] },
            SrsName = "EPSG:4326",
            Origin = new GmlCoordinate(0, 0),
            OffsetVectors = [new double[] { 1, 0 }, new double[] { 0, 1 }],
            AxisLabels = ["x", "y"]
        };

        var coverage = new GmlRectifiedGridCoverage
        {
            Id = "cov.1",
            DomainSet = grid,
            BoundedBy = new GmlEnvelope
            {
                LowerCorner = new GmlCoordinate(0, 0),
                UpperCorner = new GmlCoordinate(99, 99)
            },
            RangeSet = new GmlRangeSet { DataBlock = "1 2 3", FileReference = null },
            RangeType = new GmlRangeType
            {
                Fields = [new GmlRangeField { Name = "band1", Description = "Red", Uom = "dn" }]
            }
        };

        coverage.Id.Should().Be("cov.1");
        coverage.DomainSet.Dimension.Should().Be(2);
        coverage.DomainSet.Origin.Should().Be(new GmlCoordinate(0, 0));
        coverage.DomainSet.OffsetVectors.Should().HaveCount(2);
        coverage.DomainSet.SrsName.Should().Be("EPSG:4326");
        coverage.DomainSet.AxisLabels.Should().HaveCount(2);
        coverage.DomainSet.Limits.Low.Should().Equal(0, 0);
        coverage.DomainSet.Limits.High.Should().Equal(99, 99);
        coverage.RangeSet!.DataBlock.Should().Be("1 2 3");
        coverage.RangeSet.FileReference.Should().BeNull();
        coverage.RangeType!.Fields.Should().HaveCount(1);
        coverage.RangeType.Fields[0].Name.Should().Be("band1");
        coverage.RangeType.Fields[0].Description.Should().Be("Red");
        coverage.RangeType.Fields[0].Uom.Should().Be("dn");
        coverage.BoundedBy.Should().NotBeNull();
    }

    [Fact]
    public void GmlGridCoverage_CanBeConstructed()
    {
        var grid = new GmlGrid
        {
            Dimension = 2,
            Limits = new GmlGridEnvelope { Low = [0, 0], High = [49, 49] }
        };

        var coverage = new GmlGridCoverage
        {
            Id = "grid.1",
            DomainSet = grid
        };

        coverage.DomainSet.Dimension.Should().Be(2);
        coverage.DomainSet.AxisLabels.Should().BeEmpty();
    }

    [Fact]
    public void GmlReferenceableGridCoverage_CanBeConstructed()
    {
        var coverage = new GmlReferenceableGridCoverage
        {
            DomainSet = new GmlGrid
            {
                Dimension = 3,
                Limits = new GmlGridEnvelope { Low = [0, 0, 0], High = [9, 9, 9] }
            }
        };

        coverage.DomainSet.Dimension.Should().Be(3);
        coverage.Id.Should().BeNull();
        coverage.RangeSet.Should().BeNull();
        coverage.RangeType.Should().BeNull();
    }

    [Fact]
    public void GmlMultiPointCoverage_CanBeConstructed()
    {
        var coverage = new GmlMultiPointCoverage
        {
            DomainPoints =
            [
                new GmlPoint { Coordinate = new GmlCoordinate(1, 2) },
                new GmlPoint { Coordinate = new GmlCoordinate(3, 4) }
            ]
        };

        coverage.DomainPoints.Should().HaveCount(2);
    }

    [Fact]
    public void GmlMultiPointCoverage_WithNullDomainPoints_DefaultsToNull()
    {
        var coverage = new GmlMultiPointCoverage();
        coverage.DomainPoints.Should().BeNull();
    }

    // ---- Feature model defaults ----

    [Fact]
    public void GmlFeature_DefaultProperties_IsEmptyReadOnly()
    {
        var feature = new GmlFeature();
        feature.Properties.Should().BeEmpty();
        feature.Id.Should().BeNull();
    }

    [Fact]
    public void GmlFeatureCollection_DefaultValues()
    {
        var fc = new GmlFeatureCollection();
        fc.Features.Should().BeEmpty();
        fc.BoundedBy.Should().BeNull();
    }

    // ---- Property value types ----

    [Fact]
    public void GmlPropertyValue_AllSubtypes_CanBeConstructed()
    {
        GmlPropertyValue str = new GmlStringProperty { Value = "hello" };
        GmlPropertyValue num = new GmlNumericProperty { Value = 42.0 };
        GmlPropertyValue geom = new GmlGeometryProperty
        {
            Geometry = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) }
        };
        GmlPropertyValue nested = new GmlNestedProperty
        {
            Children = new Dictionary<string, GmlPropertyValue>
            {
                ["a"] = new GmlStringProperty { Value = "b" }
            }
        };
        GmlPropertyValue raw = new GmlRawXmlProperty { XmlContent = "<x/>" };

        str.Should().BeOfType<GmlStringProperty>();
        num.Should().BeOfType<GmlNumericProperty>();
        geom.Should().BeOfType<GmlGeometryProperty>();
        nested.Should().BeOfType<GmlNestedProperty>();
        raw.Should().BeOfType<GmlRawXmlProperty>();
    }

    // ---- GmlUnsupportedNode ----

    [Fact]
    public void GmlUnsupportedNode_CanBeConstructed()
    {
        var node = new GmlUnsupportedNode
        {
            ElementName = "Unknown",
            NamespaceUri = "http://example.com",
            RawXml = "<Unknown/>"
        };

        node.ElementName.Should().Be("Unknown");
        node.NamespaceUri.Should().Be("http://example.com");
        node.RawXml.Should().Be("<Unknown/>");
    }

    [Fact]
    public void GmlUnsupportedNode_WithNulls_DefaultsCorrectly()
    {
        var node = new GmlUnsupportedNode { ElementName = "X" };
        node.NamespaceUri.Should().BeNull();
        node.RawXml.Should().BeNull();
    }

    // ---- GmlDocument ----

    [Fact]
    public void GmlDocument_CanBeConstructedWithMinimalProperties()
    {
        var doc = new GmlDocument
        {
            Version = GmlVersion.V3_2,
            Root = new GmlPoint { Coordinate = new GmlCoordinate(1, 2) }
        };

        doc.BoundedBy.Should().BeNull();
        doc.Version.Should().Be(GmlVersion.V3_2);
    }

    // ---- GmlParseResult ----

    [Fact]
    public void GmlParseResult_WithNoIssues_HasErrorsIsFalse()
    {
        var result = new GmlParseResult();
        result.HasErrors.Should().BeFalse();
        result.Issues.Should().BeEmpty();
        result.Document.Should().BeNull();
    }

    [Fact]
    public void GmlParseResult_WithWarningOnly_HasErrorsIsFalse()
    {
        var result = new GmlParseResult
        {
            Issues = [new GmlParseIssue { Severity = GmlIssueSeverity.Warning, Code = "w", Message = "warn" }]
        };
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void GmlParseResult_WithError_HasErrorsIsTrue()
    {
        var result = new GmlParseResult
        {
            Issues = [new GmlParseIssue { Severity = GmlIssueSeverity.Error, Code = "e", Message = "err" }]
        };
        result.HasErrors.Should().BeTrue();
    }

    // ---- Geometry defaults ----

    [Fact]
    public void GmlPolygon_DefaultInterior_IsEmpty()
    {
        var poly = new GmlPolygon
        {
            Exterior = new GmlLinearRing { Coordinates = [new GmlCoordinate(0, 0)] }
        };
        poly.Interior.Should().BeEmpty();
        poly.SrsName.Should().BeNull();
        poly.Version.Should().BeNull();
    }

    [Fact]
    public void GmlSurface_DefaultPatches()
    {
        var surface = new GmlSurface { Patches = [] };
        surface.Patches.Should().BeEmpty();
    }

    [Fact]
    public void GmlCurve_DefaultCoordinates()
    {
        var curve = new GmlCurve { Coordinates = [new GmlCoordinate(1, 2)] };
        curve.Coordinates.Should().HaveCount(1);
    }

    [Fact]
    public void GmlMultiPoint_DefaultPoints()
    {
        var mp = new GmlMultiPoint { Points = [] };
        mp.Points.Should().BeEmpty();
    }

    [Fact]
    public void GmlMultiLineString_DefaultLineStrings()
    {
        var mls = new GmlMultiLineString { LineStrings = [] };
        mls.LineStrings.Should().BeEmpty();
    }

    [Fact]
    public void GmlMultiPolygon_DefaultPolygons()
    {
        var mp = new GmlMultiPolygon { Polygons = [] };
        mp.Polygons.Should().BeEmpty();
    }

    // ---- GmlVersion enum ----

    [Fact]
    public void GmlVersion_AllValues_AreDefined()
    {
        Enum.GetValues<GmlVersion>().Should().HaveCount(5);
        Enum.IsDefined(GmlVersion.V2_1_2).Should().BeTrue();
        Enum.IsDefined(GmlVersion.V3_0).Should().BeTrue();
        Enum.IsDefined(GmlVersion.V3_1).Should().BeTrue();
        Enum.IsDefined(GmlVersion.V3_2).Should().BeTrue();
        Enum.IsDefined(GmlVersion.V3_3).Should().BeTrue();
    }

    // ---- GmlIssueSeverity ----

    [Fact]
    public void GmlIssueSeverity_AllValues()
    {
        Enum.GetValues<GmlIssueSeverity>().Should().HaveCount(3);
    }

    // ---- GmlRangeSet with FileReference ----

    [Fact]
    public void GmlRangeSet_WithFileReference()
    {
        var rs = new GmlRangeSet { FileReference = "data.tif" };
        rs.DataBlock.Should().BeNull();
        rs.FileReference.Should().Be("data.tif");
    }

    // ---- GmlRangeType defaults ----

    [Fact]
    public void GmlRangeType_DefaultFields_IsEmpty()
    {
        var rt = new GmlRangeType();
        rt.Fields.Should().BeEmpty();
    }
}

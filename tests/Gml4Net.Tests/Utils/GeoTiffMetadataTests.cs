using FluentAssertions;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Geometry;
using Gml4Net.Utils;
using Xunit;

namespace Gml4Net.Tests.Utils;

public class GeoTiffMetadataTests
{
    [Fact]
    public void ExtractMetadata_FromRectifiedGridCoverage_ExtractsAll()
    {
        var coverage = CreateCoverage();

        var meta = GeoTiffUtils.ExtractMetadata(coverage);

        meta.Should().NotBeNull();
        meta!.Width.Should().Be(100);
        meta.Height.Should().Be(100);
        meta.Crs.Should().Be("EPSG:4326");
        meta.Origin.Should().Equal(0, 0);
        meta.Transform.Should().Equal(0.1, 0, 0, 0, 0.1, 0);
        meta.Resolution.Should().Equal(0.1, 0.1);
        meta.Bbox.Should().Equal(0, 0, 10, 10);
        meta.Bands.Should().Be(1);
        meta.BandInfo.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractMetadata_FromNonRectifiedCoverage_ReturnsNull()
    {
        var coverage = new GmlGridCoverage
        {
            DomainSet = new GmlGrid
            {
                Dimension = 2,
                Limits = new GmlGridEnvelope { Low = [0, 0], High = [9, 9] }
            }
        };

        var meta = GeoTiffUtils.ExtractMetadata(coverage);

        meta.Should().BeNull();
    }

    [Fact]
    public void PixelToWorld_WithValidTransform_ReturnsCorrectCoords()
    {
        var meta = GeoTiffUtils.ExtractMetadata(CreateCoverage())!;

        var world = GeoTiffUtils.PixelToWorld(50, 50, meta);

        world.Should().NotBeNull();
        world!.Value.X.Should().BeApproximately(5.0, 1e-10);
        world.Value.Y.Should().BeApproximately(5.0, 1e-10);
    }

    [Fact]
    public void PixelToWorld_AtOrigin_ReturnsOrigin()
    {
        var meta = GeoTiffUtils.ExtractMetadata(CreateCoverage())!;

        var world = GeoTiffUtils.PixelToWorld(0, 0, meta);

        world.Should().NotBeNull();
        world!.Value.X.Should().BeApproximately(0, 1e-10);
        world.Value.Y.Should().BeApproximately(0, 1e-10);
    }

    [Fact]
    public void WorldToPixel_WithValidTransform_ReturnsCorrectPixel()
    {
        var meta = GeoTiffUtils.ExtractMetadata(CreateCoverage())!;

        var pixel = GeoTiffUtils.WorldToPixel(5.0, 5.0, meta);

        pixel.Should().NotBeNull();
        pixel!.Value.Col.Should().BeApproximately(50, 1e-10);
        pixel.Value.Row.Should().BeApproximately(50, 1e-10);
    }

    [Fact]
    public void PixelToWorld_Roundtrip_IsConsistent()
    {
        var meta = GeoTiffUtils.ExtractMetadata(CreateCoverage())!;

        var world = GeoTiffUtils.PixelToWorld(25, 75, meta);
        var pixel = GeoTiffUtils.WorldToPixel(world!.Value.X, world.Value.Y, meta);

        pixel!.Value.Col.Should().BeApproximately(25, 1e-10);
        pixel.Value.Row.Should().BeApproximately(75, 1e-10);
    }

    [Fact]
    public void PixelToWorld_WithNoTransform_ReturnsNull()
    {
        var meta = new GeoTiffMetadata { Width = 10, Height = 10 };

        var result = GeoTiffUtils.PixelToWorld(0, 0, meta);

        result.Should().BeNull();
    }

    [Fact]
    public void WorldToPixel_WithNoTransform_ReturnsNull()
    {
        var meta = new GeoTiffMetadata { Width = 10, Height = 10 };

        var result = GeoTiffUtils.WorldToPixel(0, 0, meta);

        result.Should().BeNull();
    }

    [Fact]
    public void WorldToPixel_WithDegenerateTransform_ReturnsNull()
    {
        // Transform where det = 0 (singular matrix)
        var meta = new GeoTiffMetadata
        {
            Width = 10, Height = 10,
            Transform = [1, 2, 0, 2, 4, 0] // det = 1*4 - 2*2 = 0
        };

        var result = GeoTiffUtils.WorldToPixel(5, 5, meta);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMetadata_WithoutBoundedBy_BboxIsNull()
    {
        var coverage = new GmlRectifiedGridCoverage
        {
            DomainSet = new GmlRectifiedGrid
            {
                Dimension = 2,
                Limits = new GmlGridEnvelope { Low = [0, 0], High = [9, 9] },
                Origin = new GmlCoordinate(0, 0),
                OffsetVectors = [new double[] { 1, 0 }, new double[] { 0, 1 }]
            }
        };

        var meta = GeoTiffUtils.ExtractMetadata(coverage);

        meta.Should().NotBeNull();
        meta!.Bbox.Should().BeNull();
        meta.Bands.Should().BeNull();
    }

    private static GmlRectifiedGridCoverage CreateCoverage() => new()
    {
        BoundedBy = new GmlEnvelope
        {
            LowerCorner = new GmlCoordinate(0, 0),
            UpperCorner = new GmlCoordinate(10, 10)
        },
        DomainSet = new GmlRectifiedGrid
        {
            Dimension = 2,
            Limits = new GmlGridEnvelope { Low = [0, 0], High = [99, 99] },
            SrsName = "EPSG:4326",
            Origin = new GmlCoordinate(0, 0),
            OffsetVectors = [new double[] { 0.1, 0 }, new double[] { 0, 0.1 }]
        },
        RangeType = new GmlRangeType
        {
            Fields = [new GmlRangeField { Name = "elevation" }]
        }
    };
}

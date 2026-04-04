using Gml4Net.Model.Coverage;

namespace Gml4Net.Utils;

/// <summary>
/// Raster metadata extracted from a GML coverage model.
/// </summary>
public sealed class GeoTiffMetadata
{
    /// <summary>Raster width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Raster height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Bounding box [minX, minY, maxX, maxY].</summary>
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Coordinate reference system identifier.</summary>
    public string? Crs { get; init; }

    /// <summary>Affine transform coefficients [a, b, c, d, e, f].</summary>
    public IReadOnlyList<double>? Transform { get; init; }

    /// <summary>Pixel resolution [resX, resY].</summary>
    public IReadOnlyList<double>? Resolution { get; init; }

    /// <summary>Grid origin [x, y].</summary>
    public IReadOnlyList<double>? Origin { get; init; }

    /// <summary>Number of bands.</summary>
    public int? Bands { get; init; }

    /// <summary>Band information from range type.</summary>
    public IReadOnlyList<GmlRangeField>? BandInfo { get; init; }
}

/// <summary>
/// Extracts raster metadata from GML coverage models and performs
/// pixel-to-world / world-to-pixel transformations.
/// </summary>
public static class GeoTiffUtils
{
    /// <summary>
    /// Extracts raster metadata from a GML coverage.
    /// Only <see cref="GmlRectifiedGridCoverage"/> contains sufficient grid information.
    /// </summary>
    /// <param name="coverage">The coverage to extract metadata from.</param>
    /// <returns>Metadata if the coverage is a rectified grid coverage; otherwise null.</returns>
    public static GeoTiffMetadata? ExtractMetadata(GmlCoverage coverage)
    {
        ArgumentNullException.ThrowIfNull(coverage);

        if (coverage is not GmlRectifiedGridCoverage rgc)
            return null;

        var grid = rgc.DomainSet;
        var limits = grid.Limits;

        int width = limits.High.Count > 0 && limits.Low.Count > 0
            ? limits.High[0] - limits.Low[0] + 1
            : 0;
        int height = limits.High.Count > 1 && limits.Low.Count > 1
            ? limits.High[1] - limits.Low[1] + 1
            : 0;

        IReadOnlyList<double>? transform = null;
        IReadOnlyList<double>? resolution = null;

        if (grid.OffsetVectors.Count >= 2
            && grid.OffsetVectors[0].Count >= 2
            && grid.OffsetVectors[1].Count >= 2)
        {
            double a = grid.OffsetVectors[0][0]; // x-scale
            double b = grid.OffsetVectors[0][1]; // rotation
            double d = grid.OffsetVectors[1][0]; // rotation
            double e = grid.OffsetVectors[1][1]; // y-scale
            double c = grid.Origin.X;             // x-origin
            double f = grid.Origin.Y;             // y-origin

            transform = (double[])[a, b, c, d, e, f];
            resolution = (double[])[Math.Sqrt(a * a + b * b), Math.Sqrt(d * d + e * e)];
        }

        IReadOnlyList<double>? bbox = null;
        if (rgc.BoundedBy is not null)
        {
            bbox = (double[])
            [
                rgc.BoundedBy.LowerCorner.X,
                rgc.BoundedBy.LowerCorner.Y,
                rgc.BoundedBy.UpperCorner.X,
                rgc.BoundedBy.UpperCorner.Y
            ];
        }

        return new GeoTiffMetadata
        {
            Width = width,
            Height = height,
            Crs = grid.SrsName,
            Transform = transform,
            Resolution = resolution,
            Origin = (double[])[grid.Origin.X, grid.Origin.Y],
            Bbox = bbox,
            Bands = rgc.RangeType?.Fields.Count,
            BandInfo = rgc.RangeType?.Fields
        };
    }

    /// <summary>
    /// Converts pixel coordinates to world coordinates using the affine transform.
    /// </summary>
    /// <param name="col">Pixel column (x).</param>
    /// <param name="row">Pixel row (y).</param>
    /// <param name="metadata">Metadata containing the affine transform.</param>
    /// <returns>World coordinates, or null if no valid transform is available.</returns>
    public static (double X, double Y)? PixelToWorld(double col, double row, GeoTiffMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.Transform is not { Count: 6 })
            return null;

        var t = metadata.Transform;
        double x = t[0] * col + t[1] * row + t[2];
        double y = t[3] * col + t[4] * row + t[5];
        return (x, y);
    }

    /// <summary>
    /// Converts world coordinates to pixel coordinates using the inverse affine transform.
    /// </summary>
    /// <param name="x">World X coordinate.</param>
    /// <param name="y">World Y coordinate.</param>
    /// <param name="metadata">Metadata containing the affine transform.</param>
    /// <returns>Pixel coordinates, or null if no valid/invertible transform is available.</returns>
    public static (double Col, double Row)? WorldToPixel(double x, double y, GeoTiffMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        if (metadata.Transform is not { Count: 6 })
            return null;

        var t = metadata.Transform;
        double det = t[0] * t[4] - t[1] * t[3];
        if (Math.Abs(det) < 1e-15)
            return null;

        double dx = x - t[2];
        double dy = y - t[5];
        double col = (t[4] * dx - t[1] * dy) / det;
        double row = (t[0] * dy - t[3] * dx) / det;
        return (col, row);
    }
}

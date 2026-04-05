using System.Globalization;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML geometry objects to Well-Known Text (WKT) strings.
/// Implements <see cref="IBuilder{TGeometry,TFeature,TCollection}"/> and also
/// provides static convenience methods via <see cref="Instance"/>.
/// </summary>
public sealed class WktBuilder : IBuilder<string, string, string>
{
    /// <summary>Shared default instance.</summary>
    public static WktBuilder Instance { get; } = new();

    // ---- IBuilder implementation ----

    /// <inheritdoc />
    public string? BuildPoint(GmlPoint point) { ArgumentNullException.ThrowIfNull(point); return BuildPointCore(point); }
    /// <inheritdoc />
    public string? BuildLineString(GmlLineString lineString) { ArgumentNullException.ThrowIfNull(lineString); return BuildLineStringCore(lineString); }
    /// <inheritdoc />
    public string? BuildLinearRing(GmlLinearRing linearRing) { ArgumentNullException.ThrowIfNull(linearRing); return BuildLinearRingCore(linearRing); }
    /// <inheritdoc />
    public string? BuildPolygon(GmlPolygon polygon) { ArgumentNullException.ThrowIfNull(polygon); return BuildPolygonCore(polygon); }
    /// <inheritdoc />
    public string? BuildMultiPoint(GmlMultiPoint multiPoint) { ArgumentNullException.ThrowIfNull(multiPoint); return BuildMultiPointCore(multiPoint); }
    /// <inheritdoc />
    public string? BuildMultiLineString(GmlMultiLineString multiLineString) { ArgumentNullException.ThrowIfNull(multiLineString); return BuildMultiLineStringCore(multiLineString); }
    /// <inheritdoc />
    public string? BuildMultiPolygon(GmlMultiPolygon multiPolygon) { ArgumentNullException.ThrowIfNull(multiPolygon); return BuildMultiPolygonCore(multiPolygon); }
    /// <inheritdoc />
    public string? BuildEnvelope(GmlEnvelope envelope) { ArgumentNullException.ThrowIfNull(envelope); return BuildBboxWkt(envelope.LowerCorner, envelope.UpperCorner); }
    /// <inheritdoc />
    public string? BuildBox(GmlBox box) { ArgumentNullException.ThrowIfNull(box); return BuildBboxWkt(box.LowerCorner, box.UpperCorner); }
    /// <inheritdoc />
    public string? BuildCurve(GmlCurve curve) { ArgumentNullException.ThrowIfNull(curve); return BuildCurveCore(curve); }
    /// <inheritdoc />
    public string? BuildSurface(GmlSurface surface) { ArgumentNullException.ThrowIfNull(surface); return BuildSurfaceCore(surface); }

    /// <inheritdoc />
    public string BuildFeature(GmlFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        var geomEntry = feature.Properties.Entries.FirstOrDefault(e => e.Value is GmlGeometryProperty);
        if (geomEntry is null) return string.Empty;
        return Geometry(((GmlGeometryProperty)geomEntry.Value).Geometry) ?? string.Empty;
    }

    /// <inheritdoc />
    public string BuildFeatureCollection(GmlFeatureCollection fc)
    {
        ArgumentNullException.ThrowIfNull(fc);
        return string.Join("\n", fc.Features.Select(f => BuildFeature(f)));
    }

    /// <inheritdoc />
    public string? BuildCoverage(GmlCoverage coverage) => null;

    // ---- Static convenience API (backward compatible) ----

    /// <summary>
    /// Converts a <see cref="GmlGeometry"/> to a WKT string.
    /// </summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A WKT string, or null if the geometry type is not supported.</returns>
    public static string? Geometry(GmlGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return geometry switch
        {
            GmlPoint p => BuildPointCore(p),
            GmlLineString ls => BuildLineStringCore(ls),
            GmlLinearRing lr => BuildLinearRingCore(lr),
            GmlPolygon poly => BuildPolygonCore(poly),
            GmlEnvelope env => BuildBboxWkt(env.LowerCorner, env.UpperCorner),
            GmlBox box => BuildBboxWkt(box.LowerCorner, box.UpperCorner),
            GmlCurve c => BuildCurveCore(c),
            GmlSurface s => BuildSurfaceCore(s),
            GmlMultiPoint mp => BuildMultiPointCore(mp),
            GmlMultiLineString mls => BuildMultiLineStringCore(mls),
            GmlMultiPolygon mpoly => BuildMultiPolygonCore(mpoly),
            _ => null
        };
    }

    // ---- Private core builders ----

    private enum CoordinateLayout { Xy, Xyz, Xym, Xyzm }

    private static string BuildPointCore(GmlPoint p)
    {
        var c = p.Coordinate;
        var layout = GetLayout(c);
        var tag = GetTypeTag("POINT", layout);
        return $"{tag} ({FormatCoord(c, layout)})";
    }

    private static string BuildLineStringCore(GmlLineString ls) =>
        ls.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", ls.Coordinates);

    private static string BuildLinearRingCore(GmlLinearRing lr) =>
        lr.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", lr.Coordinates);

    private static string BuildPolygonCore(GmlPolygon poly)
    {
        var allCoords = new List<IReadOnlyList<GmlCoordinate>> { poly.Exterior.Coordinates };
        allCoords.AddRange(poly.Interior.Select(h => h.Coordinates));
        var layout = GetLayout(allCoords.SelectMany(r => r));
        var rings = allCoords.Select(r => $"({FormatCoords(r, layout)})");
        return $"{GetTypeTag("POLYGON", layout)} ({string.Join(", ", rings)})";
    }

    private static string BuildBboxWkt(GmlCoordinate ll, GmlCoordinate ur)
    {
        var coords = new[]
        {
            ll, new GmlCoordinate(ur.X, ll.Y, ll.Z, ll.M),
            ur, new GmlCoordinate(ll.X, ur.Y, ur.Z, ur.M), ll
        };
        var layout = GetLayout(coords);
        return $"{GetTypeTag("POLYGON", layout)} (({FormatCoords(coords, layout)}))";
    }

    private static string BuildCurveCore(GmlCurve c) =>
        c.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", c.Coordinates);

    private static string BuildSurfaceCore(GmlSurface s)
    {
        var layout = GetLayout(s.Patches.SelectMany(GetPolygonCoordinates));
        var polys = s.Patches.Select(patch => FormatPolygonRings(patch, layout));
        return $"{GetTypeTag("MULTIPOLYGON", layout)} ({string.Join(", ", polys)})";
    }

    private static string BuildMultiPointCore(GmlMultiPoint mp)
    {
        var layout = GetLayout(mp.Points.Select(p => p.Coordinate));
        var points = mp.Points.Select(p => $"({FormatCoord(p.Coordinate, layout)})");
        return $"{GetTypeTag("MULTIPOINT", layout)} ({string.Join(", ", points)})";
    }

    private static string BuildMultiLineStringCore(GmlMultiLineString mls)
    {
        var layout = GetLayout(mls.LineStrings.SelectMany(ls => ls.Coordinates));
        var lines = mls.LineStrings.Select(ls => $"({FormatCoords(ls.Coordinates, layout)})");
        return $"{GetTypeTag("MULTILINESTRING", layout)} ({string.Join(", ", lines)})";
    }

    private static string BuildMultiPolygonCore(GmlMultiPolygon mpoly)
    {
        var layout = GetLayout(mpoly.Polygons.SelectMany(GetPolygonCoordinates));
        var polys = mpoly.Polygons.Select(poly => FormatPolygonRings(poly, layout));
        return $"{GetTypeTag("MULTIPOLYGON", layout)} ({string.Join(", ", polys)})";
    }

    // ---- Shared helpers ----

    private static string FormatPolygonRings(GmlPolygon poly, CoordinateLayout layout)
    {
        var allCoords = new List<IReadOnlyList<GmlCoordinate>> { poly.Exterior.Coordinates };
        allCoords.AddRange(poly.Interior.Select(h => h.Coordinates));
        var rings = allCoords.Select(r => $"({FormatCoords(r, layout)})");
        return $"({string.Join(", ", rings)})";
    }

    private static string WrapCoords(string typeName, IReadOnlyList<GmlCoordinate> coords)
    {
        var layout = GetLayout(coords);
        return $"{GetTypeTag(typeName, layout)} ({FormatCoords(coords, layout)})";
    }

    private static string FormatCoord(GmlCoordinate c, CoordinateLayout layout) => layout switch
    {
        CoordinateLayout.Xy => $"{F(c.X)} {F(c.Y)}",
        CoordinateLayout.Xyz => $"{F(c.X)} {F(c.Y)} {F(c.Z ?? 0)}",
        CoordinateLayout.Xym => $"{F(c.X)} {F(c.Y)} {F(c.M ?? 0)}",
        CoordinateLayout.Xyzm => $"{F(c.X)} {F(c.Y)} {F(c.Z ?? 0)} {F(c.M ?? 0)}",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    private static string FormatCoords(IEnumerable<GmlCoordinate> coords, CoordinateLayout layout) =>
        string.Join(", ", coords.Select(c => FormatCoord(c, layout)));

    private static CoordinateLayout GetLayout(IEnumerable<GmlCoordinate> coords)
    {
        bool hasZ = false, hasM = false;
        foreach (var coord in coords)
        {
            hasZ |= coord.Z.HasValue;
            hasM |= coord.M.HasValue;
            if (hasZ && hasM) return CoordinateLayout.Xyzm;
        }
        if (hasM) return CoordinateLayout.Xym;
        if (hasZ) return CoordinateLayout.Xyz;
        return CoordinateLayout.Xy;
    }

    private static CoordinateLayout GetLayout(GmlCoordinate coord) => GetLayout([coord]);

    private static string GetTypeTag(string typeName, CoordinateLayout layout) => layout switch
    {
        CoordinateLayout.Xy => typeName,
        CoordinateLayout.Xyz => $"{typeName} Z",
        CoordinateLayout.Xym => $"{typeName} M",
        CoordinateLayout.Xyzm => $"{typeName} ZM",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    private static IEnumerable<GmlCoordinate> GetPolygonCoordinates(GmlPolygon polygon)
    {
        foreach (var coord in polygon.Exterior.Coordinates) yield return coord;
        foreach (var ring in polygon.Interior)
            foreach (var coord in ring.Coordinates) yield return coord;
    }

    private static string F(double d) => d.ToString(CultureInfo.InvariantCulture);
}

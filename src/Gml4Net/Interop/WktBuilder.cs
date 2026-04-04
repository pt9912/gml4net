using System.Globalization;
using Gml4Net.Model;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML geometry objects to Well-Known Text (WKT) strings.
/// </summary>
public static class WktBuilder
{
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
            GmlPoint p => BuildPoint(p),
            GmlLineString ls => BuildLineString(ls),
            GmlLinearRing lr => BuildLinearRing(lr),
            GmlPolygon poly => BuildPolygon(poly),
            GmlEnvelope env => BuildBboxWkt(env.LowerCorner, env.UpperCorner),
            GmlBox box => BuildBboxWkt(box.LowerCorner, box.UpperCorner),
            GmlCurve c => BuildCurve(c),
            GmlSurface s => BuildSurface(s),
            GmlMultiPoint mp => BuildMultiPoint(mp),
            GmlMultiLineString mls => BuildMultiLineString(mls),
            GmlMultiPolygon mpoly => BuildMultiPolygon(mpoly),
            _ => null
        };
    }

    private enum CoordinateLayout
    {
        Xy,
        Xyz,
        Xym,
        Xyzm
    }

    // ---- Builders ----

    /// <summary>Builds POINT, POINT Z, POINT M, or POINT ZM.</summary>
    private static string BuildPoint(GmlPoint p)
    {
        var c = p.Coordinate;
        var layout = GetLayout(c);
        var tag = GetTypeTag("POINT", layout);
        return $"{tag} ({FormatCoord(c, layout)})";
    }

    /// <summary>Builds LINESTRING or LINESTRING EMPTY.</summary>
    private static string BuildLineString(GmlLineString ls) =>
        ls.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", ls.Coordinates);

    /// <summary>Builds LINESTRING from LinearRing.</summary>
    private static string BuildLinearRing(GmlLinearRing lr) =>
        lr.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", lr.Coordinates);

    /// <summary>Builds POLYGON with exterior and interior rings.</summary>
    private static string BuildPolygon(GmlPolygon poly)
    {
        var allCoords = new List<IReadOnlyList<GmlCoordinate>> { poly.Exterior.Coordinates };
        allCoords.AddRange(poly.Interior.Select(h => h.Coordinates));

        var layout = GetLayout(allCoords.SelectMany(r => r));
        var rings = allCoords.Select(r =>
            $"({FormatCoords(r, layout)})");

        var tag = GetTypeTag("POLYGON", layout);
        return $"{tag} ({string.Join(", ", rings)})";
    }

    /// <summary>Builds POLYGON rectangle from lower/upper corner, preserving available ordinates.</summary>
    private static string BuildBboxWkt(GmlCoordinate ll, GmlCoordinate ur)
    {
        var coords = new[]
        {
            ll,
            new GmlCoordinate(ur.X, ll.Y, ll.Z, ll.M),
            ur,
            new GmlCoordinate(ll.X, ur.Y, ur.Z, ur.M),
            ll
        };
        var layout = GetLayout(coords);
        var tag = GetTypeTag("POLYGON", layout);
        return $"{tag} (({FormatCoords(coords, layout)}))";
    }

    /// <summary>Builds LINESTRING from Curve (flattened segments).</summary>
    private static string BuildCurve(GmlCurve c) =>
        c.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", c.Coordinates);

    /// <summary>Builds MULTIPOLYGON from Surface (polygon patches).</summary>
    private static string BuildSurface(GmlSurface s)
    {
        var layout = GetLayout(s.Patches.SelectMany(GetPolygonCoordinates));
        var polys = s.Patches.Select(patch => FormatPolygonRings(patch, layout));
        return $"{GetTypeTag("MULTIPOLYGON", layout)} ({string.Join(", ", polys)})";
    }

    /// <summary>Builds MULTIPOINT.</summary>
    private static string BuildMultiPoint(GmlMultiPoint mp)
    {
        var layout = GetLayout(mp.Points.Select(p => p.Coordinate));
        var points = mp.Points.Select(p =>
            $"({FormatCoord(p.Coordinate, layout)})");
        var tag = GetTypeTag("MULTIPOINT", layout);
        return $"{tag} ({string.Join(", ", points)})";
    }

    /// <summary>Builds MULTILINESTRING.</summary>
    private static string BuildMultiLineString(GmlMultiLineString mls)
    {
        var layout = GetLayout(mls.LineStrings.SelectMany(ls => ls.Coordinates));
        var lines = mls.LineStrings.Select(ls =>
            $"({FormatCoords(ls.Coordinates, layout)})");
        var tag = GetTypeTag("MULTILINESTRING", layout);
        return $"{tag} ({string.Join(", ", lines)})";
    }

    /// <summary>Builds MULTIPOLYGON.</summary>
    private static string BuildMultiPolygon(GmlMultiPolygon mpoly)
    {
        var layout = GetLayout(mpoly.Polygons.SelectMany(GetPolygonCoordinates));
        var polys = mpoly.Polygons.Select(poly => FormatPolygonRings(poly, layout));
        return $"{GetTypeTag("MULTIPOLYGON", layout)} ({string.Join(", ", polys)})";
    }

    // ---- Shared helpers ----

    /// <summary>Formats a polygon's exterior + interior rings as WKT ring list.</summary>
    private static string FormatPolygonRings(GmlPolygon poly, CoordinateLayout layout)
    {
        var allCoords = new List<IReadOnlyList<GmlCoordinate>> { poly.Exterior.Coordinates };
        allCoords.AddRange(poly.Interior.Select(h => h.Coordinates));

        var rings = allCoords.Select(r =>
            $"({FormatCoords(r, layout)})");
        return $"({string.Join(", ", rings)})";
    }

    /// <summary>Wraps coordinates with a WKT type tag, auto-detecting XY/XYZ/XYM/XYZM layout.</summary>
    private static string WrapCoords(string typeName, IReadOnlyList<GmlCoordinate> coords)
    {
        var layout = GetLayout(coords);
        var tag = GetTypeTag(typeName, layout);
        return $"{tag} ({FormatCoords(coords, layout)})";
    }

    // ---- Formatting helpers ----

    /// <summary>Formats a coordinate according to the requested XY/XYZ/XYM/XYZM layout.</summary>
    private static string FormatCoord(GmlCoordinate c, CoordinateLayout layout) => layout switch
    {
        CoordinateLayout.Xy => $"{F(c.X)} {F(c.Y)}",
        CoordinateLayout.Xyz => $"{F(c.X)} {F(c.Y)} {F(c.Z ?? 0)}",
        CoordinateLayout.Xym => $"{F(c.X)} {F(c.Y)} {F(c.M ?? 0)}",
        CoordinateLayout.Xyzm => $"{F(c.X)} {F(c.Y)} {F(c.Z ?? 0)} {F(c.M ?? 0)}",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    /// <summary>Formats a list of coordinates using the requested XY/XYZ/XYM/XYZM layout.</summary>
    private static string FormatCoords(IEnumerable<GmlCoordinate> coords, CoordinateLayout layout) =>
        string.Join(", ", coords.Select(c => FormatCoord(c, layout)));

    /// <summary>Determines the output layout for a coordinate sequence.</summary>
    private static CoordinateLayout GetLayout(IEnumerable<GmlCoordinate> coords)
    {
        var hasZ = false;
        var hasM = false;

        foreach (var coord in coords)
        {
            hasZ |= coord.Z.HasValue;
            hasM |= coord.M.HasValue;
            if (hasZ && hasM)
                return CoordinateLayout.Xyzm;
        }

        if (hasM)
            return CoordinateLayout.Xym;
        if (hasZ)
            return CoordinateLayout.Xyz;
        return CoordinateLayout.Xy;
    }

    /// <summary>Determines the output layout for a single coordinate.</summary>
    private static CoordinateLayout GetLayout(GmlCoordinate coord) =>
        GetLayout([coord]);

    /// <summary>Formats a geometry type tag with the required dimension suffix.</summary>
    private static string GetTypeTag(string typeName, CoordinateLayout layout) => layout switch
    {
        CoordinateLayout.Xy => typeName,
        CoordinateLayout.Xyz => $"{typeName} Z",
        CoordinateLayout.Xym => $"{typeName} M",
        CoordinateLayout.Xyzm => $"{typeName} ZM",
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    /// <summary>Enumerates all coordinates of a polygon, including holes.</summary>
    private static IEnumerable<GmlCoordinate> GetPolygonCoordinates(GmlPolygon polygon)
    {
        foreach (var coord in polygon.Exterior.Coordinates)
            yield return coord;

        foreach (var ring in polygon.Interior)
        {
            foreach (var coord in ring.Coordinates)
                yield return coord;
        }
    }

    /// <summary>Formats a double using invariant culture.</summary>
    private static string F(double d) =>
        d.ToString(CultureInfo.InvariantCulture);
}

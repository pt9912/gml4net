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

    // ---- Builders ----

    /// <summary>Builds POINT, POINT Z, or POINT ZM.</summary>
    private static string BuildPoint(GmlPoint p)
    {
        var c = p.Coordinate;
        if (c.M.HasValue && c.Z.HasValue)
            return $"POINT ZM ({FormatCoordZM(c)})";
        if (c.M.HasValue)
            return $"POINT M ({F(c.X)} {F(c.Y)} {F(c.M.Value)})";
        if (c.Z.HasValue)
            return $"POINT Z ({FormatCoord3D(c)})";
        return $"POINT ({FormatCoord(c)})";
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

        var is3D = allCoords.Any(Has3D);
        var rings = allCoords.Select(r =>
            $"({(is3D ? FormatCoords3D(r) : FormatCoords(r))})");

        var tag = is3D ? "POLYGON Z" : "POLYGON";
        return $"{tag} ({string.Join(", ", rings)})";
    }

    /// <summary>Builds POLYGON rectangle from lower/upper corner, preserving Z.</summary>
    private static string BuildBboxWkt(GmlCoordinate ll, GmlCoordinate ur)
    {
        var coords = new[]
        {
            ll, new GmlCoordinate(ur.X, ll.Y, ll.Z), ur, new GmlCoordinate(ll.X, ur.Y, ur.Z), ll
        };
        var is3D = Has3D(coords);
        var tag = is3D ? "POLYGON Z" : "POLYGON";
        return $"{tag} (({(is3D ? FormatCoords3D(coords) : FormatCoords(coords))}))";
    }

    /// <summary>Builds LINESTRING from Curve (flattened segments).</summary>
    private static string BuildCurve(GmlCurve c) =>
        c.Coordinates.Count == 0 ? "LINESTRING EMPTY" : WrapCoords("LINESTRING", c.Coordinates);

    /// <summary>Builds MULTIPOLYGON from Surface (polygon patches).</summary>
    private static string BuildSurface(GmlSurface s)
    {
        var polys = s.Patches.Select(FormatPolygonRings);
        return $"MULTIPOLYGON ({string.Join(", ", polys)})";
    }

    /// <summary>Builds MULTIPOINT.</summary>
    private static string BuildMultiPoint(GmlMultiPoint mp)
    {
        var is3D = mp.Points.Any(p => p.Coordinate.Z.HasValue);
        var points = mp.Points.Select(p =>
            is3D ? $"({FormatCoord3D(p.Coordinate)})" : $"({FormatCoord(p.Coordinate)})");
        var tag = is3D ? "MULTIPOINT Z" : "MULTIPOINT";
        return $"{tag} ({string.Join(", ", points)})";
    }

    /// <summary>Builds MULTILINESTRING.</summary>
    private static string BuildMultiLineString(GmlMultiLineString mls)
    {
        var is3D = mls.LineStrings.Any(ls => Has3D(ls.Coordinates));
        var lines = mls.LineStrings.Select(ls =>
            is3D ? $"({FormatCoords3D(ls.Coordinates)})" : $"({FormatCoords(ls.Coordinates)})");
        var tag = is3D ? "MULTILINESTRING Z" : "MULTILINESTRING";
        return $"{tag} ({string.Join(", ", lines)})";
    }

    /// <summary>Builds MULTIPOLYGON.</summary>
    private static string BuildMultiPolygon(GmlMultiPolygon mpoly)
    {
        var polys = mpoly.Polygons.Select(FormatPolygonRings);
        return $"MULTIPOLYGON ({string.Join(", ", polys)})";
    }

    // ---- Shared helpers ----

    /// <summary>Formats a polygon's exterior + interior rings as WKT ring list.</summary>
    private static string FormatPolygonRings(GmlPolygon poly)
    {
        var allCoords = new List<IReadOnlyList<GmlCoordinate>> { poly.Exterior.Coordinates };
        allCoords.AddRange(poly.Interior.Select(h => h.Coordinates));

        var is3D = allCoords.Any(Has3D);
        var rings = allCoords.Select(r =>
            $"({(is3D ? FormatCoords3D(r) : FormatCoords(r))})");
        return $"({string.Join(", ", rings)})";
    }

    /// <summary>Wraps coordinates with a WKT type tag, auto-detecting 3D.</summary>
    private static string WrapCoords(string typeName, IReadOnlyList<GmlCoordinate> coords)
    {
        var is3D = Has3D(coords);
        var tag = is3D ? $"{typeName} Z" : typeName;
        return $"{tag} ({(is3D ? FormatCoords3D(coords) : FormatCoords(coords))})";
    }

    // ---- Formatting helpers ----

    /// <summary>Formats a 2D coordinate as "x y".</summary>
    private static string FormatCoord(GmlCoordinate c) =>
        $"{F(c.X)} {F(c.Y)}";

    /// <summary>Formats a 3D coordinate as "x y z" (pads Z=0 if missing).</summary>
    private static string FormatCoord3D(GmlCoordinate c) =>
        $"{F(c.X)} {F(c.Y)} {F(c.Z ?? 0)}";

    /// <summary>Formats a 4D coordinate as "x y z m".</summary>
    private static string FormatCoordZM(GmlCoordinate c) =>
        $"{F(c.X)} {F(c.Y)} {F(c.Z ?? 0)} {F(c.M ?? 0)}";

    /// <summary>Formats a list of 2D coordinates as comma-separated pairs.</summary>
    private static string FormatCoords(IEnumerable<GmlCoordinate> coords) =>
        string.Join(", ", coords.Select(FormatCoord));

    /// <summary>Formats a list of 3D coordinates as comma-separated triples.</summary>
    private static string FormatCoords3D(IEnumerable<GmlCoordinate> coords) =>
        string.Join(", ", coords.Select(FormatCoord3D));

    /// <summary>Returns true if any coordinate in the list has a Z component.</summary>
    private static bool Has3D(IReadOnlyList<GmlCoordinate> coords) =>
        coords.Any(c => c.Z.HasValue);

    /// <summary>Formats a double using invariant culture.</summary>
    private static string F(double d) =>
        d.ToString(CultureInfo.InvariantCulture);
}

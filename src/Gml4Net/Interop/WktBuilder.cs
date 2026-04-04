using System.Globalization;
using System.Text;
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
            GmlEnvelope env => BuildEnvelope(env),
            GmlBox box => BuildBox(box),
            GmlCurve c => BuildCurve(c),
            GmlSurface s => BuildSurface(s),
            GmlMultiPoint mp => BuildMultiPoint(mp),
            GmlMultiLineString mls => BuildMultiLineString(mls),
            GmlMultiPolygon mpoly => BuildMultiPolygon(mpoly),
            _ => null
        };
    }

    // ---- Builders ----

    /// <summary>Builds POINT (x y) or POINT Z (x y z).</summary>
    private static string BuildPoint(GmlPoint p) =>
        p.Coordinate.Z.HasValue
            ? $"POINT Z ({FormatCoord3D(p.Coordinate)})"
            : $"POINT ({FormatCoord(p.Coordinate)})";

    /// <summary>Builds LINESTRING (x y, x y, ...).</summary>
    private static string BuildLineString(GmlLineString ls) =>
        Has3D(ls.Coordinates)
            ? $"LINESTRING Z ({FormatCoords3D(ls.Coordinates)})"
            : $"LINESTRING ({FormatCoords(ls.Coordinates)})";

    /// <summary>Builds LINESTRING from LinearRing (same WKT output).</summary>
    private static string BuildLinearRing(GmlLinearRing lr) =>
        Has3D(lr.Coordinates)
            ? $"LINESTRING Z ({FormatCoords3D(lr.Coordinates)})"
            : $"LINESTRING ({FormatCoords(lr.Coordinates)})";

    /// <summary>Builds POLYGON ((x y, ...), (x y, ...)).</summary>
    private static string BuildPolygon(GmlPolygon poly)
    {
        var rings = new List<string> { $"({FormatRingCoords(poly.Exterior.Coordinates)})" };
        foreach (var hole in poly.Interior)
            rings.Add($"({FormatRingCoords(hole.Coordinates)})");

        var is3D = Has3D(poly.Exterior.Coordinates);
        return is3D
            ? $"POLYGON Z ({string.Join(", ", rings)})"
            : $"POLYGON ({string.Join(", ", rings)})";
    }

    /// <summary>Builds POLYGON rectangle from Envelope.</summary>
    private static string BuildEnvelope(GmlEnvelope env)
    {
        var ll = env.LowerCorner;
        var ur = env.UpperCorner;
        var coords = new[]
        {
            ll, new GmlCoordinate(ur.X, ll.Y), ur, new GmlCoordinate(ll.X, ur.Y), ll
        };
        return $"POLYGON (({FormatCoords(coords)}))";
    }

    /// <summary>Builds POLYGON rectangle from GML 2 Box.</summary>
    private static string BuildBox(GmlBox box)
    {
        var ll = box.LowerCorner;
        var ur = box.UpperCorner;
        var coords = new[]
        {
            ll, new GmlCoordinate(ur.X, ll.Y), ur, new GmlCoordinate(ll.X, ur.Y), ll
        };
        return $"POLYGON (({FormatCoords(coords)}))";
    }

    /// <summary>Builds LINESTRING from Curve (flattened segments).</summary>
    private static string BuildCurve(GmlCurve c) =>
        Has3D(c.Coordinates)
            ? $"LINESTRING Z ({FormatCoords3D(c.Coordinates)})"
            : $"LINESTRING ({FormatCoords(c.Coordinates)})";

    /// <summary>Builds MULTIPOLYGON from Surface (polygon patches).</summary>
    private static string BuildSurface(GmlSurface s)
    {
        var polys = s.Patches.Select(p =>
        {
            var rings = new List<string> { $"({FormatRingCoords(p.Exterior.Coordinates)})" };
            foreach (var hole in p.Interior)
                rings.Add($"({FormatRingCoords(hole.Coordinates)})");
            return $"({string.Join(", ", rings)})";
        });
        return $"MULTIPOLYGON ({string.Join(", ", polys)})";
    }

    /// <summary>Builds MULTIPOINT ((x y), (x y), ...).</summary>
    private static string BuildMultiPoint(GmlMultiPoint mp)
    {
        var is3D = mp.Points.Any(p => p.Coordinate.Z.HasValue);
        var points = mp.Points.Select(p =>
            is3D ? $"({FormatCoord3D(p.Coordinate)})" : $"({FormatCoord(p.Coordinate)})");
        return is3D
            ? $"MULTIPOINT Z ({string.Join(", ", points)})"
            : $"MULTIPOINT ({string.Join(", ", points)})";
    }

    /// <summary>Builds MULTILINESTRING ((x y, ...), ...).</summary>
    private static string BuildMultiLineString(GmlMultiLineString mls)
    {
        var is3D = mls.LineStrings.Any(ls => Has3D(ls.Coordinates));
        var lines = mls.LineStrings.Select(ls =>
            is3D ? $"({FormatCoords3D(ls.Coordinates)})" : $"({FormatCoords(ls.Coordinates)})");
        return is3D
            ? $"MULTILINESTRING Z ({string.Join(", ", lines)})"
            : $"MULTILINESTRING ({string.Join(", ", lines)})";
    }

    /// <summary>Builds MULTIPOLYGON (((x y, ...)), ...).</summary>
    private static string BuildMultiPolygon(GmlMultiPolygon mpoly)
    {
        var polys = mpoly.Polygons.Select(poly =>
        {
            var rings = new List<string> { $"({FormatRingCoords(poly.Exterior.Coordinates)})" };
            foreach (var hole in poly.Interior)
                rings.Add($"({FormatRingCoords(hole.Coordinates)})");
            return $"({string.Join(", ", rings)})";
        });
        return $"MULTIPOLYGON ({string.Join(", ", polys)})";
    }

    // ---- Formatting helpers ----

    /// <summary>Formats a 2D coordinate as "x y".</summary>
    private static string FormatCoord(GmlCoordinate c) =>
        $"{F(c.X)} {F(c.Y)}";

    /// <summary>Formats a 3D coordinate as "x y z".</summary>
    private static string FormatCoord3D(GmlCoordinate c) =>
        c.Z.HasValue ? $"{F(c.X)} {F(c.Y)} {F(c.Z.Value)}" : $"{F(c.X)} {F(c.Y)}";

    /// <summary>Formats a list of 2D coordinates as comma-separated pairs.</summary>
    private static string FormatCoords(IEnumerable<GmlCoordinate> coords) =>
        string.Join(", ", coords.Select(FormatCoord));

    /// <summary>Formats a list of 3D coordinates as comma-separated triples.</summary>
    private static string FormatCoords3D(IEnumerable<GmlCoordinate> coords) =>
        string.Join(", ", coords.Select(FormatCoord3D));

    /// <summary>Formats ring coordinates, choosing 2D or 3D based on content.</summary>
    private static string FormatRingCoords(IReadOnlyList<GmlCoordinate> coords) =>
        Has3D(coords) ? FormatCoords3D(coords) : FormatCoords(coords);

    /// <summary>Returns true if any coordinate in the list has a Z component.</summary>
    private static bool Has3D(IReadOnlyList<GmlCoordinate> coords) =>
        coords.Any(c => c.Z.HasValue);

    /// <summary>Formats a double using invariant culture.</summary>
    private static string F(double d) =>
        d.ToString(CultureInfo.InvariantCulture);
}

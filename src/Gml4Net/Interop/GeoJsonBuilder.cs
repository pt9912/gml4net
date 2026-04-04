using System.Globalization;
using System.Text.Json.Nodes;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML model objects to GeoJSON using <see cref="System.Text.Json.Nodes"/>.
/// </summary>
public static class GeoJsonBuilder
{
    /// <summary>
    /// Converts a <see cref="GmlGeometry"/> to a GeoJSON geometry <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A GeoJSON geometry object, or null if the geometry type is not supported.</returns>
    public static JsonObject? Geometry(GmlGeometry geometry)
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

    /// <summary>
    /// Converts a <see cref="GmlFeature"/> to a GeoJSON Feature <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="feature">The GML feature to convert.</param>
    /// <returns>A GeoJSON Feature object.</returns>
    public static JsonObject Feature(GmlFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        JsonNode? geometryNode = null;
        var properties = new JsonObject();

        foreach (var (key, value) in feature.Properties)
        {
            if (value is GmlGeometryProperty gp && geometryNode is null)
            {
                geometryNode = Geometry(gp.Geometry);
            }
            else
            {
                properties[key] = ConvertPropertyValue(value);
            }
        }

        var result = new JsonObject
        {
            ["type"] = "Feature",
            ["geometry"] = geometryNode,
            ["properties"] = properties
        };

        if (feature.Id is not null)
            result["id"] = feature.Id;

        return result;
    }

    /// <summary>
    /// Converts a <see cref="GmlFeatureCollection"/> to a GeoJSON FeatureCollection <see cref="JsonObject"/>.
    /// </summary>
    /// <param name="fc">The GML feature collection to convert.</param>
    /// <returns>A GeoJSON FeatureCollection object.</returns>
    public static JsonObject FeatureCollection(GmlFeatureCollection fc)
    {
        ArgumentNullException.ThrowIfNull(fc);

        var features = new JsonArray();
        foreach (var f in fc.Features)
        {
            features.Add(Feature(f));
        }

        return new JsonObject
        {
            ["type"] = "FeatureCollection",
            ["features"] = features
        };
    }

    /// <summary>
    /// Auto-dispatches a <see cref="GmlDocument"/> to the appropriate GeoJSON representation.
    /// </summary>
    /// <param name="document">The parsed GML document.</param>
    /// <returns>A GeoJSON object, or null if the root content is not convertible.</returns>
    public static JsonObject? Document(GmlDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Root switch
        {
            GmlFeatureCollection fc => FeatureCollection(fc),
            GmlFeature f => Feature(f),
            GmlGeometry g => Geometry(g),
            _ => null
        };
    }

    /// <summary>
    /// Converts a <see cref="GmlGeometry"/> to a GeoJSON JSON string.
    /// </summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A GeoJSON geometry JSON string, or null if not supported.</returns>
    public static string? GeometryToJson(GmlGeometry geometry) =>
        Geometry(geometry)?.ToJsonString();

    /// <summary>
    /// Converts a <see cref="GmlFeature"/> to a GeoJSON Feature JSON string.
    /// </summary>
    /// <param name="feature">The GML feature to convert.</param>
    /// <returns>A GeoJSON Feature JSON string.</returns>
    public static string FeatureToJson(GmlFeature feature) =>
        Feature(feature).ToJsonString();

    /// <summary>
    /// Converts a <see cref="GmlFeatureCollection"/> to a GeoJSON FeatureCollection JSON string.
    /// </summary>
    /// <param name="fc">The GML feature collection to convert.</param>
    /// <returns>A GeoJSON FeatureCollection JSON string.</returns>
    public static string FeatureCollectionToJson(GmlFeatureCollection fc) =>
        FeatureCollection(fc).ToJsonString();

    // ---- Private builders ----

    /// <summary>Builds a GeoJSON Point from a GML Point.</summary>
    private static JsonObject BuildPoint(GmlPoint p) => new()
    {
        ["type"] = "Point",
        ["coordinates"] = CoordToArray(p.Coordinate)
    };

    /// <summary>Builds a GeoJSON LineString from a GML LineString.</summary>
    private static JsonObject BuildLineString(GmlLineString ls) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = CoordsToArray(ls.Coordinates)
    };

    /// <summary>Builds a GeoJSON LineString from a GML LinearRing (same structure).</summary>
    private static JsonObject BuildLinearRing(GmlLinearRing lr) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = CoordsToArray(lr.Coordinates)
    };

    /// <summary>Builds a GeoJSON Polygon from a GML Polygon.</summary>
    private static JsonObject BuildPolygon(GmlPolygon poly)
    {
        var rings = new JsonArray { CoordsToArray(poly.Exterior.Coordinates) };
        foreach (var hole in poly.Interior)
            rings.Add(CoordsToArray(hole.Coordinates));

        return new JsonObject
        {
            ["type"] = "Polygon",
            ["coordinates"] = rings
        };
    }

    /// <summary>Builds a GeoJSON Polygon rectangle from a GML Envelope.</summary>
    private static JsonObject BuildEnvelope(GmlEnvelope env)
    {
        var ll = env.LowerCorner;
        var ur = env.UpperCorner;
        var ring = new JsonArray
        {
            CoordToArray(ll),
            CoordToArray(new GmlCoordinate(ur.X, ll.Y)),
            CoordToArray(ur),
            CoordToArray(new GmlCoordinate(ll.X, ur.Y)),
            CoordToArray(ll)
        };

        return new JsonObject
        {
            ["type"] = "Polygon",
            ["coordinates"] = new JsonArray { ring }
        };
    }

    /// <summary>Builds a GeoJSON Polygon rectangle from a GML 2 Box.</summary>
    private static JsonObject BuildBox(GmlBox box)
    {
        var ll = box.LowerCorner;
        var ur = box.UpperCorner;
        var ring = new JsonArray
        {
            CoordToArray(ll),
            CoordToArray(new GmlCoordinate(ur.X, ll.Y)),
            CoordToArray(ur),
            CoordToArray(new GmlCoordinate(ll.X, ur.Y)),
            CoordToArray(ll)
        };

        return new JsonObject
        {
            ["type"] = "Polygon",
            ["coordinates"] = new JsonArray { ring }
        };
    }

    /// <summary>Builds a GeoJSON LineString from a GML Curve (flattened).</summary>
    private static JsonObject BuildCurve(GmlCurve c) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = CoordsToArray(c.Coordinates)
    };

    /// <summary>Builds a GeoJSON MultiPolygon from a GML Surface (polygon patches).</summary>
    private static JsonObject BuildSurface(GmlSurface s)
    {
        var polygons = new JsonArray();
        foreach (var patch in s.Patches)
        {
            var rings = new JsonArray { CoordsToArray(patch.Exterior.Coordinates) };
            foreach (var hole in patch.Interior)
                rings.Add(CoordsToArray(hole.Coordinates));
            polygons.Add(rings);
        }

        return new JsonObject
        {
            ["type"] = "MultiPolygon",
            ["coordinates"] = polygons
        };
    }

    /// <summary>Builds a GeoJSON MultiPoint from a GML MultiPoint.</summary>
    private static JsonObject BuildMultiPoint(GmlMultiPoint mp) => new()
    {
        ["type"] = "MultiPoint",
        ["coordinates"] = new JsonArray(mp.Points.Select(p => CoordToArray(p.Coordinate)).ToArray<JsonNode>())
    };

    /// <summary>Builds a GeoJSON MultiLineString from a GML MultiLineString.</summary>
    private static JsonObject BuildMultiLineString(GmlMultiLineString mls) => new()
    {
        ["type"] = "MultiLineString",
        ["coordinates"] = new JsonArray(mls.LineStrings.Select(ls => CoordsToArray(ls.Coordinates)).ToArray<JsonNode>())
    };

    /// <summary>Builds a GeoJSON MultiPolygon from a GML MultiPolygon.</summary>
    private static JsonObject BuildMultiPolygon(GmlMultiPolygon mpoly)
    {
        var polygons = new JsonArray();
        foreach (var poly in mpoly.Polygons)
        {
            var rings = new JsonArray { CoordsToArray(poly.Exterior.Coordinates) };
            foreach (var hole in poly.Interior)
                rings.Add(CoordsToArray(hole.Coordinates));
            polygons.Add(rings);
        }

        return new JsonObject
        {
            ["type"] = "MultiPolygon",
            ["coordinates"] = polygons
        };
    }

    // ---- Coordinate helpers ----

    /// <summary>Converts a single coordinate to a JSON array [x, y] or [x, y, z].</summary>
    private static JsonArray CoordToArray(GmlCoordinate c)
    {
        var arr = new JsonArray { Num(c.X), Num(c.Y) };
        if (c.Z.HasValue)
            arr.Add(Num(c.Z.Value));
        return arr;
    }

    /// <summary>Converts a list of coordinates to a JSON array of arrays.</summary>
    private static JsonArray CoordsToArray(IReadOnlyList<GmlCoordinate> coords) =>
        new(coords.Select(c => CoordToArray(c)).ToArray<JsonNode>());

    /// <summary>Creates a JsonValue from a double, using integer representation when possible.</summary>
    private static JsonValue Num(double d) =>
        d == Math.Truncate(d) ? JsonValue.Create((long)d) : JsonValue.Create(d);

    // ---- Property conversion ----

    /// <summary>Converts a GML property value to its JSON equivalent.</summary>
    private static JsonNode? ConvertPropertyValue(GmlPropertyValue value) => value switch
    {
        GmlStringProperty s => JsonValue.Create(s.Value),
        GmlNumericProperty n => Num(n.Value),
        GmlGeometryProperty g => Geometry(g.Geometry),
        GmlNestedProperty nested => ConvertNestedProperty(nested),
        GmlRawXmlProperty raw => JsonValue.Create(raw.XmlContent),
        _ => null
    };

    /// <summary>Converts a nested GML property to a JSON object.</summary>
    private static JsonObject ConvertNestedProperty(GmlNestedProperty nested)
    {
        var obj = new JsonObject();
        foreach (var (key, val) in nested.Children)
        {
            obj[key] = ConvertPropertyValue(val);
        }
        return obj;
    }
}

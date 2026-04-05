using System.Text.Json.Nodes;
using Gml4Net.Model;
using Gml4Net.Model.Coverage;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Interop;

/// <summary>
/// Converts GML model objects to GeoJSON using <see cref="System.Text.Json.Nodes"/>.
/// Implements <see cref="IBuilder{TGeometry,TFeature,TCollection}"/> and also
/// provides static convenience methods via <see cref="Instance"/>.
/// </summary>
public sealed class GeoJsonBuilder : IBuilder<JsonObject, JsonObject, JsonObject>
{
    /// <summary>Shared default instance.</summary>
    public static GeoJsonBuilder Instance { get; } = new();

    // ---- IBuilder implementation ----

    /// <inheritdoc />
    public JsonObject? BuildPoint(GmlPoint point) { ArgumentNullException.ThrowIfNull(point); return BuildPointCore(point); }
    /// <inheritdoc />
    public JsonObject? BuildLineString(GmlLineString lineString) { ArgumentNullException.ThrowIfNull(lineString); return BuildLineStringCore(lineString); }
    /// <inheritdoc />
    public JsonObject? BuildLinearRing(GmlLinearRing linearRing) { ArgumentNullException.ThrowIfNull(linearRing); return BuildLinearRingCore(linearRing); }
    /// <inheritdoc />
    public JsonObject? BuildPolygon(GmlPolygon polygon) { ArgumentNullException.ThrowIfNull(polygon); return BuildPolygonCore(polygon); }
    /// <inheritdoc />
    public JsonObject? BuildMultiPoint(GmlMultiPoint multiPoint) { ArgumentNullException.ThrowIfNull(multiPoint); return BuildMultiPointCore(multiPoint); }
    /// <inheritdoc />
    public JsonObject? BuildMultiLineString(GmlMultiLineString multiLineString) { ArgumentNullException.ThrowIfNull(multiLineString); return BuildMultiLineStringCore(multiLineString); }
    /// <inheritdoc />
    public JsonObject? BuildMultiPolygon(GmlMultiPolygon multiPolygon) { ArgumentNullException.ThrowIfNull(multiPolygon); return BuildMultiPolygonCore(multiPolygon); }
    /// <inheritdoc />
    public JsonObject? BuildEnvelope(GmlEnvelope envelope) { ArgumentNullException.ThrowIfNull(envelope); return BuildBboxPolygon(envelope.LowerCorner, envelope.UpperCorner); }
    /// <inheritdoc />
    public JsonObject? BuildBox(GmlBox box) { ArgumentNullException.ThrowIfNull(box); return BuildBboxPolygon(box.LowerCorner, box.UpperCorner); }
    /// <inheritdoc />
    public JsonObject? BuildCurve(GmlCurve curve) { ArgumentNullException.ThrowIfNull(curve); return BuildCurveCore(curve); }
    /// <inheritdoc />
    public JsonObject? BuildSurface(GmlSurface surface) { ArgumentNullException.ThrowIfNull(surface); return BuildSurfaceCore(surface); }
    /// <inheritdoc />
    public JsonObject BuildFeature(GmlFeature feature) => Feature(feature);
    /// <inheritdoc />
    public JsonObject BuildFeatureCollection(GmlFeatureCollection fc) => FeatureCollection(fc);
    /// <inheritdoc />
    public JsonObject? BuildCoverage(GmlCoverage coverage) => null;

    // ---- Static convenience API (backward compatible) ----

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
            GmlPoint p => BuildPointCore(p),
            GmlLineString ls => BuildLineStringCore(ls),
            GmlLinearRing lr => BuildLinearRingCore(lr),
            GmlPolygon poly => BuildPolygonCore(poly),
            GmlEnvelope env => BuildBboxPolygon(env.LowerCorner, env.UpperCorner),
            GmlBox box => BuildBboxPolygon(box.LowerCorner, box.UpperCorner),
            GmlCurve c => BuildCurveCore(c),
            GmlSurface s => BuildSurfaceCore(s),
            GmlMultiPoint mp => BuildMultiPointCore(mp),
            GmlMultiLineString mls => BuildMultiLineStringCore(mls),
            GmlMultiPolygon mpoly => BuildMultiPolygonCore(mpoly),
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

        foreach (var entry in feature.Properties.Entries)
        {
            if (entry.Value is GmlGeometryProperty gp && geometryNode is null)
            {
                geometryNode = Geometry(gp.Geometry);
            }
            else
            {
                AppendPropertyValue(properties, entry.Name, ConvertPropertyValue(entry.Value));
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
            features.Add(Feature(f));

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

    /// <summary>Converts a geometry to a GeoJSON JSON string.</summary>
    /// <param name="geometry">The GML geometry to convert.</param>
    /// <returns>A GeoJSON geometry JSON string, or null if not supported.</returns>
    public static string? GeometryToJson(GmlGeometry geometry) =>
        Geometry(geometry)?.ToJsonString();

    /// <summary>Converts a feature to a GeoJSON Feature JSON string.</summary>
    /// <param name="feature">The GML feature to convert.</param>
    /// <returns>A GeoJSON Feature JSON string.</returns>
    public static string FeatureToJson(GmlFeature feature) =>
        Feature(feature).ToJsonString();

    /// <summary>Converts a feature collection to a GeoJSON FeatureCollection JSON string.</summary>
    /// <param name="fc">The GML feature collection to convert.</param>
    /// <returns>A GeoJSON FeatureCollection JSON string.</returns>
    public static string FeatureCollectionToJson(GmlFeatureCollection fc) =>
        FeatureCollection(fc).ToJsonString();

    // ---- Private core builders ----

    private static JsonObject BuildPointCore(GmlPoint p) => new()
    {
        ["type"] = "Point",
        ["coordinates"] = CoordToArray(p.Coordinate)
    };

    private static JsonObject BuildLineStringCore(GmlLineString ls) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = CoordsToArray(ls.Coordinates)
    };

    private static JsonObject BuildLinearRingCore(GmlLinearRing lr) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = CoordsToArray(lr.Coordinates)
    };

    private static JsonObject BuildPolygonCore(GmlPolygon poly)
    {
        var rings = new JsonArray { CoordsToArray(poly.Exterior.Coordinates) };
        foreach (var hole in poly.Interior)
            rings.Add(CoordsToArray(hole.Coordinates));
        return new JsonObject { ["type"] = "Polygon", ["coordinates"] = rings };
    }

    private static JsonObject BuildBboxPolygon(GmlCoordinate ll, GmlCoordinate ur)
    {
        var ring = new JsonArray
        {
            CoordToArray(ll),
            CoordToArray(new GmlCoordinate(ur.X, ll.Y, ll.Z, ll.M)),
            CoordToArray(ur),
            CoordToArray(new GmlCoordinate(ll.X, ur.Y, ur.Z, ur.M)),
            CoordToArray(ll)
        };
        return new JsonObject { ["type"] = "Polygon", ["coordinates"] = new JsonArray { ring } };
    }

    private static JsonObject BuildCurveCore(GmlCurve c) => new()
    {
        ["type"] = "LineString",
        ["coordinates"] = CoordsToArray(c.Coordinates)
    };

    private static JsonObject BuildSurfaceCore(GmlSurface s)
    {
        var polygons = new JsonArray();
        foreach (var patch in s.Patches)
        {
            var rings = new JsonArray { CoordsToArray(patch.Exterior.Coordinates) };
            foreach (var hole in patch.Interior)
                rings.Add(CoordsToArray(hole.Coordinates));
            polygons.Add(rings);
        }
        return new JsonObject { ["type"] = "MultiPolygon", ["coordinates"] = polygons };
    }

    private static JsonObject BuildMultiPointCore(GmlMultiPoint mp) => new()
    {
        ["type"] = "MultiPoint",
        ["coordinates"] = new JsonArray(mp.Points.Select(p => CoordToArray(p.Coordinate)).ToArray<JsonNode>())
    };

    private static JsonObject BuildMultiLineStringCore(GmlMultiLineString mls) => new()
    {
        ["type"] = "MultiLineString",
        ["coordinates"] = new JsonArray(mls.LineStrings.Select(ls => CoordsToArray(ls.Coordinates)).ToArray<JsonNode>())
    };

    private static JsonObject BuildMultiPolygonCore(GmlMultiPolygon mpoly)
    {
        var polygons = new JsonArray();
        foreach (var poly in mpoly.Polygons)
        {
            var rings = new JsonArray { CoordsToArray(poly.Exterior.Coordinates) };
            foreach (var hole in poly.Interior)
                rings.Add(CoordsToArray(hole.Coordinates));
            polygons.Add(rings);
        }
        return new JsonObject { ["type"] = "MultiPolygon", ["coordinates"] = polygons };
    }

    // ---- Coordinate helpers ----

    private static JsonArray CoordToArray(GmlCoordinate c)
    {
        var arr = new JsonArray { Num(c.X), Num(c.Y) };
        if (c.Z.HasValue) arr.Add(Num(c.Z.Value));
        if (c.M.HasValue) arr.Add(Num(c.M.Value));
        return arr;
    }

    private static JsonArray CoordsToArray(IReadOnlyList<GmlCoordinate> coords) =>
        new(coords.Select(c => CoordToArray(c)).ToArray<JsonNode>());

    private static JsonValue Num(double d) =>
        d == Math.Truncate(d) && d >= long.MinValue && d <= long.MaxValue && !double.IsInfinity(d)
            ? JsonValue.Create((long)d)
            : JsonValue.Create(d);

    // ---- Property conversion ----

    private static JsonNode? ConvertPropertyValue(GmlPropertyValue value) => value switch
    {
        GmlStringProperty s => JsonValue.Create(s.Value),
        GmlNumericProperty n => Num(n.Value),
        GmlGeometryProperty g => Geometry(g.Geometry),
        GmlNestedProperty nested => ConvertNestedProperty(nested),
        GmlRawXmlProperty raw => JsonValue.Create(raw.XmlContent),
        _ => null
    };

    private static JsonObject ConvertNestedProperty(GmlNestedProperty nested)
    {
        var obj = new JsonObject();
        foreach (var entry in nested.Children.Entries)
            AppendPropertyValue(obj, entry.Name, ConvertPropertyValue(entry.Value));
        return obj;
    }

    private static void AppendPropertyValue(JsonObject obj, string name, JsonNode? value)
    {
        if (!obj.TryGetPropertyValue(name, out var existing))
        {
            obj[name] = value;
            return;
        }
        if (existing is JsonArray array)
        {
            array.Add(value);
            return;
        }
        obj[name] = new JsonArray(existing?.DeepClone(), value);
    }
}

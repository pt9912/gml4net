using System.Globalization;
using System.Xml.Linq;
using Gml4Net.Model;

namespace Gml4Net.Parser;

/// <summary>
/// Internal helper methods for namespace handling, element lookup and coordinate parsing.
/// </summary>
internal static class XmlHelpers
{
    /// <summary>
    /// Returns true if the namespace URI is a known GML namespace.
    /// </summary>
    internal static bool IsGmlNamespace(string? ns) =>
        ns == GmlNamespaces.Gml || ns == GmlNamespaces.Gml32 || ns == GmlNamespaces.Gml33;

    /// <summary>
    /// Returns true if the namespace URI is a known WFS namespace.
    /// </summary>
    internal static bool IsWfsNamespace(string? ns) =>
        ns == GmlNamespaces.Wfs1 || ns == GmlNamespaces.Wfs2;

    /// <summary>
    /// Finds a direct child element with the given local name in any GML namespace.
    /// </summary>
    internal static XElement? FindGmlChild(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e =>
            e.Name.LocalName == localName && IsGmlNamespace(e.Name.NamespaceName));
    }

    /// <summary>
    /// Finds all direct child elements with the given local name in any GML namespace.
    /// </summary>
    internal static IEnumerable<XElement> FindGmlChildren(XElement parent, string localName)
    {
        return parent.Elements().Where(e =>
            e.Name.LocalName == localName && IsGmlNamespace(e.Name.NamespaceName));
    }

    /// <summary>
    /// Finds all direct child elements with the given local name in any WFS namespace.
    /// </summary>
    internal static IEnumerable<XElement> FindWfsChildren(XElement parent, string localName)
    {
        return parent.Elements().Where(e =>
            e.Name.LocalName == localName && IsWfsNamespace(e.Name.NamespaceName));
    }

    /// <summary>
    /// Extracts the srsName attribute from an element.
    /// </summary>
    internal static string? GetSrsName(XElement element) =>
        element.Attribute("srsName")?.Value;

    /// <summary>
    /// Extracts the feature ID from gml:id or fid attribute.
    /// </summary>
    internal static string? GetFeatureId(XElement element) =>
        element.Attribute(GmlNamespaces.NsGml32 + "id")?.Value
        ?? element.Attribute(GmlNamespaces.NsGml + "id")?.Value
        ?? element.Attribute("id")?.Value
        ?? element.Attribute("fid")?.Value;

    /// <summary>
    /// Extracts the srsDimension attribute, defaulting to the given value.
    /// </summary>
    internal static int GetSrsDimension(XElement element, int defaultValue = 2)
    {
        var attr = element.Attribute("srsDimension");
        if (attr is not null && int.TryParse(attr.Value, out var dim))
            return dim;
        return defaultValue;
    }

    /// <summary>
    /// Detects the GML version from namespace declarations in the document.
    /// </summary>
    internal static GmlVersion DetectVersion(XDocument doc)
    {
        var root = doc.Root;
        if (root is null)
            return GmlVersion.V3_2;

        // Collect all namespace URIs from the document
        var namespaces = root.DescendantsAndSelf()
            .SelectMany(e => e.Attributes().Where(a => a.IsNamespaceDeclaration).Select(a => a.Value))
            .ToHashSet();

        // Also check element namespaces
        foreach (var el in root.DescendantsAndSelf())
        {
            namespaces.Add(el.Name.NamespaceName);
        }

        if (namespaces.Contains(GmlNamespaces.Gml33))
            return GmlVersion.V3_3;

        if (namespaces.Contains(GmlNamespaces.Gml32))
            return GmlVersion.V3_2;

        if (namespaces.Contains(GmlNamespaces.Gml))
        {
            // Distinguish GML 2 from GML 3.0/3.1 via content heuristics
            if (HasGml2Indicators(root))
                return GmlVersion.V2_1_2;

            return GmlVersion.V3_1;
        }

        return GmlVersion.V3_2;
    }

    private static bool HasGml2Indicators(XElement root)
    {
        // GML 2 indicators: <coordinates>, <Box>, <outerBoundaryIs>
        foreach (var el in root.DescendantsAndSelf())
        {
            if (!IsGmlNamespace(el.Name.NamespaceName))
                continue;

            var localName = el.Name.LocalName;
            if (localName is "coordinates" or "Box" or "outerBoundaryIs")
                return true;
        }
        return false;
    }

    /// <summary>
    /// Parses a GML 3 pos element value (e.g. "10.0 20.0") into a coordinate.
    /// </summary>
    internal static GmlCoordinate ParsePos(string text, int? srsDimension = null)
    {
        var parts = text.Trim().Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
            return new GmlCoordinate(0, 0);

        double x = double.Parse(parts[0], CultureInfo.InvariantCulture);
        double y = double.Parse(parts[1], CultureInfo.InvariantCulture);
        double? z = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : null;
        double? m = parts.Length > 3 ? double.Parse(parts[3], CultureInfo.InvariantCulture) : null;

        // If srsDimension is 2, drop Z even if present
        if (srsDimension == 2)
            return new GmlCoordinate(x, y);

        return new GmlCoordinate(x, y, z, m);
    }

    /// <summary>
    /// Parses a GML 3 posList element value into a list of coordinates.
    /// </summary>
    internal static IReadOnlyList<GmlCoordinate> ParsePosList(string text, int srsDimension)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return [];

        var parts = text.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var values = new List<double>(parts.Length);
        foreach (var part in parts)
        {
            values.Add(double.Parse(part, CultureInfo.InvariantCulture));
        }

        if (srsDimension < 2) srsDimension = 2;

        var coords = new List<GmlCoordinate>(values.Count / srsDimension);
        for (int i = 0; i + srsDimension <= values.Count; i += srsDimension)
        {
            double x = values[i];
            double y = values[i + 1];
            double? z = srsDimension >= 3 && i + 2 < values.Count ? values[i + 2] : null;
            double? m = srsDimension >= 4 && i + 3 < values.Count ? values[i + 3] : null;
            coords.Add(new GmlCoordinate(x, y, z, m));
        }

        return coords;
    }

    /// <summary>
    /// Parses a GML 2 coordinates element value (e.g. "10.0,20.0 30.0,40.0").
    /// Default separator is comma for coordinate parts and space/newline between tuples.
    /// </summary>
    internal static IReadOnlyList<GmlCoordinate> ParseGml2Coordinates(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text))
            return [];

        var tuples = text.Split((char[])[' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var coords = new List<GmlCoordinate>(tuples.Length);

        foreach (var tuple in tuples)
        {
            var parts = tuple.Split(',');
            if (parts.Length < 2) continue;

            double x = double.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            double y = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            double? z = parts.Length > 2 ? double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture) : null;

            coords.Add(new GmlCoordinate(x, y, z));
        }

        return coords;
    }
}

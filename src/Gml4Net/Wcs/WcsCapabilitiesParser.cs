using System.Xml.Linq;
using Gml4Net.Parser;

namespace Gml4Net.Wcs;

/// <summary>
/// Parsed WCS GetCapabilities result.
/// </summary>
public sealed class WcsCapabilities
{
    /// <summary>WCS protocol version.</summary>
    public required string Version { get; init; }

    /// <summary>Service identification metadata.</summary>
    public WcsServiceIdentification? ServiceIdentification { get; init; }

    /// <summary>Advertised operations with GET/POST endpoints.</summary>
    public IReadOnlyList<WcsOperationMetadata> Operations { get; init; } = [];

    /// <summary>Available coverages.</summary>
    public IReadOnlyList<WcsCoverageSummary> Coverages { get; init; } = [];

    /// <summary>Supported output formats.</summary>
    public IReadOnlyList<string> Formats { get; init; } = [];

    /// <summary>Supported coordinate reference systems.</summary>
    public IReadOnlyList<string> Crs { get; init; } = [];
}

/// <summary>
/// WCS service identification metadata.
/// </summary>
public sealed class WcsServiceIdentification
{
    /// <summary>Service title.</summary>
    public string? Title { get; init; }

    /// <summary>Service abstract / description.</summary>
    public string? Abstract { get; init; }

    /// <summary>Keywords describing the service.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];
}

/// <summary>
/// A WCS operation endpoint.
/// </summary>
public sealed class WcsOperationMetadata
{
    /// <summary>Operation name (e.g. "GetCoverage").</summary>
    public required string Name { get; init; }

    /// <summary>HTTP GET endpoint URL.</summary>
    public string? GetUrl { get; init; }

    /// <summary>HTTP POST endpoint URL.</summary>
    public string? PostUrl { get; init; }
}

/// <summary>
/// A coverage summary from a WCS GetCapabilities response.
/// </summary>
public sealed class WcsCoverageSummary
{
    /// <summary>Coverage identifier.</summary>
    public required string CoverageId { get; init; }

    /// <summary>Coverage subtype (e.g. "RectifiedGridCoverage").</summary>
    public string? Subtype { get; init; }

    /// <summary>Bounding box as [minX, minY, maxX, maxY].</summary>
    public IReadOnlyList<double>? Bbox { get; init; }
}

/// <summary>
/// Parses WCS GetCapabilities XML responses (WCS 1.x and 2.x).
/// </summary>
public static class WcsCapabilitiesParser
{
    private static readonly XNamespace NsOws = GmlNamespaces.Ows;
    private static readonly XNamespace NsWcs = GmlNamespaces.Wcs;

    /// <summary>
    /// Parses a WCS GetCapabilities XML string.
    /// </summary>
    /// <param name="xml">The Capabilities XML string.</param>
    /// <returns>The parsed capabilities.</returns>
    public static WcsCapabilities Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var doc = XDocument.Parse(xml);
        var root = doc.Root!;

        var version = root.Attribute("version")?.Value ?? "unknown";
        var serviceId = ParseServiceIdentification(root);
        var operations = ParseOperations(root);
        var coverages = ParseCoverageSummaries(root);
        var formats = ParseFormats(root);
        var crs = ParseCrs(root);

        return new WcsCapabilities
        {
            Version = version,
            ServiceIdentification = serviceId,
            Operations = operations,
            Coverages = coverages,
            Formats = formats,
            Crs = crs
        };
    }

    /// <summary>Parses ows:ServiceIdentification.</summary>
    private static WcsServiceIdentification? ParseServiceIdentification(XElement root)
    {
        var siEl = root.Element(NsOws + "ServiceIdentification");
        if (siEl is null) return null;

        var title = siEl.Element(NsOws + "Title")?.Value;
        var abs = siEl.Element(NsOws + "Abstract")?.Value;
        var keywords = siEl.Descendants(NsOws + "Keyword")
            .Select(k => k.Value.Trim())
            .Where(k => !string.IsNullOrEmpty(k))
            .ToList();

        return new WcsServiceIdentification
        {
            Title = title,
            Abstract = abs,
            Keywords = keywords
        };
    }

    /// <summary>Parses ows:OperationsMetadata.</summary>
    private static IReadOnlyList<WcsOperationMetadata> ParseOperations(XElement root)
    {
        var opsMeta = root.Element(NsOws + "OperationsMetadata");
        if (opsMeta is null) return [];

        var ops = new List<WcsOperationMetadata>();
        foreach (var opEl in opsMeta.Elements(NsOws + "Operation"))
        {
            var name = opEl.Attribute("name")?.Value;
            if (name is null) continue;

            string? getUrl = null;
            string? postUrl = null;

            foreach (var dcpEl in opEl.Elements(NsOws + "DCP"))
            {
                var httpEl = dcpEl.Element(NsOws + "HTTP");
                if (httpEl is null) continue;

                getUrl ??= httpEl.Element(NsOws + "Get")?.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href")?.Value;
                postUrl ??= httpEl.Element(NsOws + "Post")?.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href")?.Value;
            }

            ops.Add(new WcsOperationMetadata { Name = name, GetUrl = getUrl, PostUrl = postUrl });
        }

        return ops;
    }

    /// <summary>Parses wcs:Contents/CoverageSummary elements.</summary>
    private static IReadOnlyList<WcsCoverageSummary> ParseCoverageSummaries(XElement root)
    {
        var contentsEl = root.Element(NsWcs + "Contents");
        if (contentsEl is null) return [];

        var summaries = new List<WcsCoverageSummary>();
        foreach (var csEl in contentsEl.Elements(NsWcs + "CoverageSummary"))
        {
            var covId = csEl.Element(NsWcs + "CoverageId")?.Value
                     ?? csEl.Element(NsWcs + "Identifier")?.Value;
            if (covId is null) continue;

            var subtype = csEl.Element(NsWcs + "CoverageSubtype")?.Value;

            IReadOnlyList<double>? bbox = null;
            var bboxEl = csEl.Element(NsOws + "WGS84BoundingBox")
                      ?? csEl.Element(NsOws + "BoundingBox");
            if (bboxEl is not null)
            {
                bbox = ParseOwsBbox(bboxEl);
            }

            summaries.Add(new WcsCoverageSummary
            {
                CoverageId = covId,
                Subtype = subtype,
                Bbox = bbox
            });
        }

        return summaries;
    }

    /// <summary>Parses supported formats from ServiceMetadata or Contents.</summary>
    private static IReadOnlyList<string> ParseFormats(XElement root)
    {
        // WCS 2.0: wcs:ServiceMetadata/wcs:formatSupported
        var smEl = root.Element(NsWcs + "ServiceMetadata");
        if (smEl is not null)
        {
            return smEl.Elements(NsWcs + "formatSupported")
                .Select(f => f.Value.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
        }

        return [];
    }

    /// <summary>Parses supported CRS from ServiceMetadata or Contents.</summary>
    private static IReadOnlyList<string> ParseCrs(XElement root)
    {
        var smEl = root.Element(NsWcs + "ServiceMetadata");
        if (smEl is not null)
        {
            // WCS 2.0 extension: crsSupported
            var crsEls = smEl.Elements()
                .Where(e => e.Name.LocalName == "crsSupported")
                .Select(e => e.Value.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
            if (crsEls.Count > 0) return crsEls;
        }

        return [];
    }

    /// <summary>Parses an OWS BoundingBox into [minX, minY, maxX, maxY].</summary>
    private static IReadOnlyList<double>? ParseOwsBbox(XElement bboxEl)
    {
        var lower = bboxEl.Element(NsOws + "LowerCorner")?.Value;
        var upper = bboxEl.Element(NsOws + "UpperCorner")?.Value;
        if (lower is null || upper is null) return null;

        var lParts = lower.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var uParts = upper.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (lParts.Length < 2 || uParts.Length < 2) return null;

        if (double.TryParse(lParts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var minX)
            && double.TryParse(lParts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var minY)
            && double.TryParse(uParts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var maxX)
            && double.TryParse(uParts[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var maxY))
        {
            return (double[])[minX, minY, maxX, maxY];
        }

        return null;
    }
}

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
        var serviceIdEl = Child(root, "ServiceIdentification");
        if (serviceIdEl is not null)
        {
            return new WcsServiceIdentification
            {
                Title = ChildValue(serviceIdEl, "Title"),
                Abstract = ChildValue(serviceIdEl, "Abstract"),
                Keywords = Descendants(serviceIdEl, "Keyword")
                    .Select(k => k.Value.Trim())
                    .Where(k => !string.IsNullOrEmpty(k))
                    .ToList()
            };
        }

        var serviceEl = Child(root, "Service");
        if (serviceEl is null)
            return null;

        var title = ChildValue(serviceEl, "label") ?? ChildValue(serviceEl, "name");
        var abs = ChildValue(serviceEl, "description");
        var keywords = Descendants(serviceEl, "Keyword")
            .Concat(Descendants(serviceEl, "keyword"))
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
        var opsMeta = Child(root, "OperationsMetadata");
        if (opsMeta is not null)
        {
            var ops = new List<WcsOperationMetadata>();
            foreach (var opEl in Children(opsMeta, "Operation"))
            {
                var name = opEl.Attribute("name")?.Value;
                if (name is null) continue;

                string? getUrl = null;
                string? postUrl = null;

                foreach (var dcpEl in Children(opEl, "DCP"))
                {
                    var httpEl = Child(dcpEl, "HTTP");
                    if (httpEl is null) continue;

                    getUrl ??= GetRequestUrl(Child(httpEl, "Get"));
                    postUrl ??= GetRequestUrl(Child(httpEl, "Post"));
                }

                ops.Add(new WcsOperationMetadata { Name = name, GetUrl = getUrl, PostUrl = postUrl });
            }

            return ops;
        }

        var capabilityEl = Child(root, "Capability");
        var requestEl = capabilityEl is not null ? Child(capabilityEl, "Request") : null;
        if (requestEl is null)
            return [];

        var legacyOps = new List<WcsOperationMetadata>();
        foreach (var opEl in requestEl.Elements())
        {
            var name = opEl.Name.LocalName;
            string? getUrl = null;
            string? postUrl = null;

            foreach (var dcpEl in Children(opEl, "DCPType"))
            {
                var httpEl = Child(dcpEl, "HTTP");
                if (httpEl is null) continue;

                getUrl ??= GetRequestUrl(Child(httpEl, "Get"));
                postUrl ??= GetRequestUrl(Child(httpEl, "Post"));
            }

            legacyOps.Add(new WcsOperationMetadata { Name = name, GetUrl = getUrl, PostUrl = postUrl });
        }

        return legacyOps;
    }

    /// <summary>Parses wcs:Contents/CoverageSummary elements.</summary>
    private static IReadOnlyList<WcsCoverageSummary> ParseCoverageSummaries(XElement root)
    {
        var summaries = new List<WcsCoverageSummary>();

        var contentsEl = Child(root, "Contents");
        if (contentsEl is not null)
        {
            foreach (var csEl in Children(contentsEl, "CoverageSummary"))
            {
                var summary = ParseCoverageSummary(csEl);
                if (summary is not null)
                    summaries.Add(summary);
            }
        }

        var contentMetadataEl = Child(root, "ContentMetadata");
        if (contentMetadataEl is not null)
        {
            foreach (var csEl in contentMetadataEl.Elements()
                         .Where(e => e.Name.LocalName is "CoverageOfferingBrief" or "CoverageOfferingSummary"))
            {
                var summary = ParseCoverageSummary(csEl);
                if (summary is not null)
                    summaries.Add(summary);
            }
        }

        return summaries;
    }

    /// <summary>Parses supported formats from ServiceMetadata or Contents.</summary>
    private static IReadOnlyList<string> ParseFormats(XElement root)
    {
        var smEl = Child(root, "ServiceMetadata");
        if (smEl is not null)
        {
            var formats = Children(smEl, "formatSupported")
                .Select(f => f.Value.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
            if (formats.Count > 0) return formats;
        }

        var capabilityEl = Child(root, "Capability");
        var requestEl = capabilityEl is not null ? Child(capabilityEl, "Request") : null;
        var getCoverageEl = requestEl is not null ? Child(requestEl, "GetCoverage") : null;
        if (getCoverageEl is not null)
        {
            return Children(getCoverageEl, "Format")
                .Select(f => f.Value.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return [];
    }

    /// <summary>Parses supported CRS from ServiceMetadata or Contents.</summary>
    private static IReadOnlyList<string> ParseCrs(XElement root)
    {
        var smEl = Child(root, "ServiceMetadata");
        if (smEl is not null)
        {
            var crsEls = smEl.Elements()
                .Where(e => e.Name.LocalName == "crsSupported")
                .Select(e => e.Value.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
            if (crsEls.Count > 0) return crsEls;
        }

        return root
            .Descendants()
            .Where(e => e.Name.LocalName is "requestResponseCRSs" or "responseCRSs" or "nativeCRSs" or "supportedCRSs")
            .SelectMany(e => e.Value
                .Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries))
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static WcsCoverageSummary? ParseCoverageSummary(XElement summaryEl)
    {
        var covId = ChildValue(summaryEl, "CoverageId")
                 ?? ChildValue(summaryEl, "Identifier")
                 ?? ChildValue(summaryEl, "name");
        if (string.IsNullOrWhiteSpace(covId))
            return null;

        var subtype = ChildValue(summaryEl, "CoverageSubtype");

        IReadOnlyList<double>? bbox = null;
        var bboxEl = summaryEl.Elements().FirstOrDefault(e =>
            e.Name.LocalName is "WGS84BoundingBox" or "BoundingBox" or "lonLatEnvelope" or "Envelope");
        if (bboxEl is not null)
            bbox = ParseBbox(bboxEl);

        return new WcsCoverageSummary
        {
            CoverageId = covId,
            Subtype = subtype,
            Bbox = bbox
        };
    }

    private static IReadOnlyList<double>? ParseBbox(XElement bboxEl)
    {
        var lower = ChildValue(bboxEl, "LowerCorner");
        var upper = ChildValue(bboxEl, "UpperCorner");
        if (lower is not null && upper is not null)
            return ParseCornerBbox(lower, upper);

        var posEls = bboxEl.Elements().Where(e => e.Name.LocalName == "pos").ToList();
        if (posEls.Count >= 2)
            return ParseCornerBbox(posEls[0].Value, posEls[1].Value);

        var coordsEl = ChildValue(bboxEl, "coordinates");
        if (coordsEl is not null)
        {
            var coords = XmlHelpers.ParseGml2Coordinates(coordsEl);
            if (coords.Count >= 2)
                return (double[])[coords[0].X, coords[0].Y, coords[1].X, coords[1].Y];
        }

        return null;
    }

    private static IReadOnlyList<double>? ParseCornerBbox(string lower, string upper)
    {
        var lParts = lower.Trim().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var uParts = upper.Trim().Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lParts.Length < 2 || uParts.Length < 2)
            return null;

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

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static IEnumerable<XElement> Children(XElement parent, string localName) =>
        parent.Elements().Where(e => e.Name.LocalName == localName);

    private static IEnumerable<XElement> Descendants(XElement parent, string localName) =>
        parent.Descendants().Where(e => e.Name.LocalName == localName);

    private static string? ChildValue(XElement parent, string localName) =>
        Child(parent, localName)?.Value?.Trim();

    private static string? GetRequestUrl(XElement? methodEl)
    {
        if (methodEl is null)
            return null;

        return methodEl.Attribute(XNamespace.Get("http://www.w3.org/1999/xlink") + "href")?.Value
            ?? methodEl.Attribute("onlineResource")?.Value;
    }
}

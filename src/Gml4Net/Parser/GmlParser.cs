using System.Xml.Linq;
using Gml4Net.Interop;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;

namespace Gml4Net.Parser;

/// <summary>
/// Main entry point for parsing GML documents.
/// </summary>
public static class GmlParser
{
    /// <summary>
    /// Creates a generic parser that combines parsing and builder dispatch in a single step.
    /// Type parameters are inferred from the builder instance.
    /// </summary>
    /// <param name="builder">The builder to use for converting parsed GML to the target format.</param>
    /// <returns>A new <see cref="GmlParser{TGeometry,TFeature,TCollection}"/> instance.</returns>
    public static GmlParser<TGeometry, TFeature, TCollection>
        Create<TGeometry, TFeature, TCollection>(
            IBuilder<TGeometry, TFeature, TCollection> builder)
        => new(builder);

    /// <summary>
    /// Parses a GML XML string into a <see cref="GmlParseResult"/>.
    /// </summary>
    /// <param name="xml">The GML XML string to parse.</param>
    /// <returns>A result containing the parsed document and any diagnostic issues.</returns>
    public static GmlParseResult ParseXmlString(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var issues = new List<GmlParseIssue>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_xml",
                Message = $"Invalid XML: {ex.Message}",
                Location = ex.LineNumber > 0 ? $"Line {ex.LineNumber}, Position {ex.LinePosition}" : null
            });
            return new GmlParseResult { Issues = issues };
        }

        return ParseDocument(doc, issues);
    }

    /// <summary>
    /// Parses a GML document from a byte span.
    /// Uses XDocument.Load to respect the XML encoding declaration.
    /// </summary>
    /// <param name="bytes">The raw bytes of the GML XML document.</param>
    /// <returns>A result containing the parsed document and any diagnostic issues.</returns>
    public static GmlParseResult ParseBytes(ReadOnlySpan<byte> bytes)
    {
        var issues = new List<GmlParseIssue>();
        using var stream = new MemoryStream(bytes.ToArray());

        XDocument doc;
        try
        {
            doc = XDocument.Load(stream);
        }
        catch (System.Xml.XmlException ex)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_xml",
                Message = $"Invalid XML: {ex.Message}",
                Location = ex.LineNumber > 0 ? $"Line {ex.LineNumber}, Position {ex.LinePosition}" : null
            });
            return new GmlParseResult { Issues = issues };
        }

        return ParseDocument(doc, issues);
    }

    /// <summary>
    /// Parses a GML document from a stream.
    /// </summary>
    /// <param name="stream">A stream containing the GML XML document.</param>
    /// <returns>A result containing the parsed document and any diagnostic issues.</returns>
    public static GmlParseResult ParseStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var issues = new List<GmlParseIssue>();

        XDocument doc;
        try
        {
            doc = XDocument.Load(stream);
        }
        catch (System.Xml.XmlException ex)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "invalid_xml",
                Message = $"Invalid XML: {ex.Message}",
                Location = ex.LineNumber > 0 ? $"Line {ex.LineNumber}, Position {ex.LinePosition}" : null
            });
            return new GmlParseResult { Issues = issues };
        }

        return ParseDocument(doc, issues);
    }

    /// <summary>
    /// Parses a validated XDocument into a <see cref="GmlParseResult"/>, detecting the GML version and dispatching the root element.
    /// </summary>
    private static GmlParseResult ParseDocument(XDocument doc, List<GmlParseIssue> issues)
    {
        var root = doc.Root;
        if (root is null)
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "empty_document",
                Message = "XML document has no root element"
            });
            return new GmlParseResult { Issues = issues };
        }

        var version = XmlHelpers.DetectVersion(doc);
        var rootContent = DispatchRoot(root, version, issues);

        if (rootContent is null && !issues.Any(i => i.Severity == GmlIssueSeverity.Error))
        {
            issues.Add(new GmlParseIssue
            {
                Severity = GmlIssueSeverity.Error,
                Code = "unknown_root",
                Message = $"Unrecognized root element: {root.Name.LocalName} ({root.Name.NamespaceName})",
                Location = root.Name.LocalName
            });
        }

        if (rootContent is null)
            return new GmlParseResult { Issues = issues };

        // Extract boundedBy if present (skip for FeatureCollection — it handles its own)
        GmlEnvelope? boundedBy = null;
        if (rootContent is not GmlFeatureCollection)
        {
            var boundedByEl = XmlHelpers.FindGmlChild(root, "boundedBy");
            if (boundedByEl is not null)
            {
                var envEl = XmlHelpers.FindGmlChild(boundedByEl, "Envelope");
                if (envEl is not null)
                {
                    boundedBy = GeometryParser.Parse(envEl, version, issues) as GmlEnvelope;
                }
                else
                {
                    var boxEl = XmlHelpers.FindGmlChild(boundedByEl, "Box");
                    if (boxEl is not null)
                    {
                        var box = GeometryParser.Parse(boxEl, version, issues) as GmlBox;
                        if (box is not null)
                        {
                            boundedBy = new GmlEnvelope
                            {
                                LowerCorner = box.LowerCorner,
                                UpperCorner = box.UpperCorner,
                                SrsName = box.SrsName
                            };
                        }
                    }
                }
            }
        }

        var document = new GmlDocument
        {
            Version = version,
            Root = rootContent,
            BoundedBy = boundedBy
        };

        return new GmlParseResult { Document = document, Issues = issues };
    }

    /// <summary>
    /// Routes the root element to the appropriate parser based on its namespace and local name.
    /// </summary>
    private static IGmlRootContent? DispatchRoot(XElement root, GmlVersion version, List<GmlParseIssue> issues)
    {
        var ns = root.Name.NamespaceName;
        var localName = root.Name.LocalName;

        // Geometry types
        if (XmlHelpers.IsGmlNamespace(ns) && XmlHelpers.IsGeometryElement(localName))
        {
            return GeometryParser.Parse(root, version, issues);
        }

        // FeatureCollection (GML or WFS namespace only)
        if ((XmlHelpers.IsGmlNamespace(ns) || XmlHelpers.IsWfsNamespace(ns))
            && localName == "FeatureCollection")
        {
            return FeatureParser.ParseCollection(root, version, issues);
        }

        // Coverage types
        if ((XmlHelpers.IsGmlNamespace(ns) || ns == GmlNamespaces.Gmlcov) && IsCoverageElement(localName))
        {
            return CoverageParser.Parse(root, version, issues);
        }

        // Single feature: non-GML/WFS root that looks like a feature
        // Requires gml:id or fid attribute, or contains a GML geometry child
        if (!XmlHelpers.IsGmlNamespace(ns) && !XmlHelpers.IsWfsNamespace(ns)
            && root.HasElements && LooksLikeFeature(root))
        {
            return FeatureParser.ParseFeature(root, version, issues);
        }

        return null;
    }

    /// <summary>
    /// Determines whether an element appears to be a GML feature by checking for a feature ID or GML geometry descendants.
    /// </summary>
    private static bool LooksLikeFeature(XElement element)
    {
        // Has a feature ID attribute
        if (XmlHelpers.GetFeatureId(element) is not null)
            return true;

        // Contains at least one child with a GML geometry grandchild
        foreach (var child in element.Elements())
        {
            foreach (var grandchild in child.Elements())
            {
                if (XmlHelpers.IsGmlNamespace(grandchild.Name.NamespaceName)
                    && XmlHelpers.IsGeometryElement(grandchild.Name.LocalName))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if the local name matches a known GML coverage element type.
    /// </summary>
    private static bool IsCoverageElement(string localName) => localName is
        "RectifiedGridCoverage" or "GridCoverage" or
        "ReferenceableGridCoverage" or "MultiPointCoverage";
}

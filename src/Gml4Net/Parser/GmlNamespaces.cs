using System.Xml.Linq;

namespace Gml4Net.Parser;

/// <summary>
/// GML and related OGC namespace constants.
/// </summary>
internal static class GmlNamespaces
{
    // GML 2.1.2, 3.0 and 3.1 share the same namespace.
    // Version detection uses content heuristics (e.g. <coordinates>, <Box>).
    internal const string Gml = "http://www.opengis.net/gml";
    internal const string Gml32 = "http://www.opengis.net/gml/3.2";
    internal const string Gml33 = "http://www.opengis.net/gml/3.3";
    internal const string Wfs1 = "http://www.opengis.net/wfs";
    internal const string Wfs2 = "http://www.opengis.net/wfs/2.0";
    internal const string Swe = "http://www.opengis.net/swe/2.0";
    internal const string Gmlcov = "http://www.opengis.net/gmlcov/1.0";
    internal const string Ows = "http://www.opengis.net/ows/1.1";
    internal const string Wcs = "http://www.opengis.net/wcs/2.0";

    internal static readonly XNamespace NsGml = Gml;
    internal static readonly XNamespace NsGml32 = Gml32;
    internal static readonly XNamespace NsGml33 = Gml33;
    internal static readonly XNamespace NsWfs1 = Wfs1;
    internal static readonly XNamespace NsWfs2 = Wfs2;
}

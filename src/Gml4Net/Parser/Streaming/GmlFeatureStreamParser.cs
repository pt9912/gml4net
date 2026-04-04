using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using Gml4Net.Model;
using Gml4Net.Model.Feature;

namespace Gml4Net.Parser.Streaming;

/// <summary>
/// Streams GML features from large documents using forward-only XmlReader,
/// keeping memory constant regardless of document size.
/// </summary>
public static class GmlFeatureStreamParser
{
    /// <summary>
    /// Asynchronously streams features from a GML/WFS document.
    /// Each feature is parsed via the DOM-based FeatureParser from an XElement fragment.
    /// </summary>
    /// <param name="stream">The input stream containing the GML/WFS XML document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of parsed features.</returns>
    public static async IAsyncEnumerable<GmlFeature> ParseAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true
        });

        var version = GmlVersion.V3_2;
        var versionDetected = false;

        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            if (reader.NodeType != XmlNodeType.Element)
                continue;

            // Detect version from first GML namespace encountered
            if (!versionDetected)
            {
                version = DetectVersionFromReader(reader);
                versionDetected = true;
            }

            if (IsFeatureMemberElement(reader))
            {
                // Read into the member element, find the feature child
                if (await reader.ReadAsync().ConfigureAwait(false)
                    && reader.NodeType == XmlNodeType.Element
                    && !IsGmlOrWfsElement(reader))
                {
                    var featureEl = (XElement)await XNode.ReadFromAsync(reader, ct).ConfigureAwait(false);
                    var issues = new List<GmlParseIssue>();
                    var feature = FeatureParser.ParseFeature(featureEl, version, issues);
                    if (feature is not null)
                        yield return feature;
                }
            }
            else if (IsFeatureMembersElement(reader))
            {
                // featureMembers (plural): read the entire element as XElement,
                // then iterate children via DOM. This avoids XmlReader state issues
                // with ReadSubtree losing namespace context.
                var membersEl = (XElement)await XNode.ReadFromAsync(reader, ct).ConfigureAwait(false);
                var issues = new List<GmlParseIssue>();
                foreach (var child in membersEl.Elements())
                {
                    ct.ThrowIfCancellationRequested();
                    if (XmlHelpers.IsGmlNamespace(child.Name.NamespaceName)
                        || XmlHelpers.IsWfsNamespace(child.Name.NamespaceName))
                        continue;

                    var feature = FeatureParser.ParseFeature(child, version, issues);
                    if (feature is not null)
                        yield return feature;
                }
            }
        }
    }

    /// <summary>
    /// Processes features from a GML/WFS stream using a callback.
    /// </summary>
    /// <param name="stream">The input stream containing the GML/WFS XML document.</param>
    /// <param name="onFeature">Callback invoked for each parsed feature.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of features processed.</returns>
    public static async Task<int> ProcessFeaturesAsync(
        Stream stream,
        Func<GmlFeature, Task> onFeature,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(onFeature);

        int count = 0;
        await foreach (var feature in ParseAsync(stream, ct).ConfigureAwait(false))
        {
            await onFeature(feature).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    /// <summary>Detects GML version from namespace URIs on the current element.</summary>
    private static GmlVersion DetectVersionFromReader(XmlReader reader)
    {
        if (reader.NamespaceURI == GmlNamespaces.Gml33)
            return GmlVersion.V3_3;
        if (reader.NamespaceURI == GmlNamespaces.Gml32)
            return GmlVersion.V3_2;

        // Check namespace declarations
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (reader.Value == GmlNamespaces.Gml33) return GmlVersion.V3_3;
                if (reader.Value == GmlNamespaces.Gml32) return GmlVersion.V3_2;
            } while (reader.MoveToNextAttribute());
            reader.MoveToElement();
        }

        return GmlVersion.V3_2;
    }

    /// <summary>Returns true if the current element is a feature member wrapper (singular).</summary>
    private static bool IsFeatureMemberElement(XmlReader reader) =>
        reader.LocalName == "featureMember" && XmlHelpers.IsGmlNamespace(reader.NamespaceURI)
        || reader.LocalName == "member" && XmlHelpers.IsWfsNamespace(reader.NamespaceURI);

    /// <summary>Returns true if the current element is featureMembers (plural).</summary>
    private static bool IsFeatureMembersElement(XmlReader reader) =>
        reader.LocalName == "featureMembers" && XmlHelpers.IsGmlNamespace(reader.NamespaceURI);

    /// <summary>Returns true if the current element is in a GML or WFS namespace.</summary>
    private static bool IsGmlOrWfsElement(XmlReader reader) =>
        XmlHelpers.IsGmlNamespace(reader.NamespaceURI) || XmlHelpers.IsWfsNamespace(reader.NamespaceURI);
}

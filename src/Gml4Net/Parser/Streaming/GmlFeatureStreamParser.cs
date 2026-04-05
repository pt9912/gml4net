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

        using var reader = CreateReader(stream);

        await foreach (var feature in ParseAsync(reader, ct).ConfigureAwait(false))
        {
            yield return feature;
        }
    }

    /// <summary>
    /// Creates a configured forward-only XML reader for streaming parsing.
    /// </summary>
    internal static XmlReader CreateReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true
        });
    }

    /// <summary>
    /// Asynchronously streams features from an existing XML reader.
    /// The reader may already be positioned on the document root element.
    /// </summary>
    internal static async IAsyncEnumerable<GmlFeature> ParseAsync(
        XmlReader reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        await foreach (var item in ParseItemsCore(reader, ct).ConfigureAwait(false))
        {
            if (item.IsSuccess)
                yield return item.Feature!;
            else if (!item.CanContinue)
                throw item.Exception ?? new InvalidOperationException("Fatal streaming parse error.");
        }
    }

    /// <summary>
    /// Asynchronously streams feature parse results including error information.
    /// Used internally by <see cref="StreamingGmlParser"/> for error-aware streaming.
    /// </summary>
    internal static async IAsyncEnumerable<FeatureStreamItem> ParseItemsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = CreateReader(stream);

        await foreach (var item in ParseItemsCore(reader, ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Core implementation that yields <see cref="FeatureStreamItem"/> for each
    /// feature parse attempt. Recoverable errors produce items with
    /// <see cref="FeatureStreamItem.CanContinue"/> = true; fatal errors produce
    /// items with CanContinue = false.
    /// </summary>
    private static async IAsyncEnumerable<FeatureStreamItem> ParseItemsCore(
        XmlReader reader,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var version = GmlVersion.V3_2;
        var versionDetected = false;
        var processCurrentElement = reader.NodeType == XmlNodeType.Element;

        while (processCurrentElement || await reader.ReadAsync().ConfigureAwait(false))
        {
            processCurrentElement = false;
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
                    var (featureEl, fatalError) = await TryReadFragmentAsync(reader, ct).ConfigureAwait(false);
                    if (fatalError is not null)
                    {
                        yield return fatalError;
                        yield break;
                    }

                    yield return ParseFeatureElement(featureEl!, version);
                }
            }
            else if (IsFeatureMembersElement(reader))
            {
                var membersDepth = reader.Depth;
                var alreadyPositioned = false;

                while (alreadyPositioned || await reader.ReadAsync().ConfigureAwait(false))
                {
                    alreadyPositioned = false;
                    ct.ThrowIfCancellationRequested();

                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == membersDepth)
                        break;

                    if (reader.NodeType == XmlNodeType.Element
                        && reader.Depth == membersDepth + 1
                        && !IsGmlOrWfsElement(reader))
                    {
                        var (featureEl, fatalError) = await TryReadFragmentAsync(reader, ct).ConfigureAwait(false);
                        if (fatalError is not null)
                        {
                            yield return fatalError;
                            yield break;
                        }

                        yield return ParseFeatureElement(featureEl!, version);

                        // ReadFromAsync positioned the reader on the next node already
                        alreadyPositioned = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Tries to read an XML fragment. Returns the element on success, or a fatal
    /// <see cref="FeatureStreamItem"/> on failure. OperationCanceledException is
    /// not caught and propagates to the caller.
    /// </summary>
    private static async Task<(XElement? Element, FeatureStreamItem? FatalError)> TryReadFragmentAsync(
        XmlReader reader, CancellationToken ct)
    {
        try
        {
            var element = (XElement)await XNode.ReadFromAsync(reader, ct).ConfigureAwait(false);
            return (element, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return (null, new FeatureStreamItem { Exception = ex, CanContinue = false });
        }
    }

    /// <summary>
    /// Parses a materialized feature XElement into a <see cref="FeatureStreamItem"/>.
    /// Since the fragment is already read, any error here is recoverable.
    /// </summary>
    private static FeatureStreamItem ParseFeatureElement(XElement featureEl, GmlVersion version)
    {
        var effectiveVersion = XmlHelpers.DetectVersion(featureEl, version);
        var issues = new List<GmlParseIssue>();

        try
        {
            var feature = FeatureParser.ParseFeature(featureEl, effectiveVersion, issues);
            if (feature is not null)
                return new FeatureStreamItem { Feature = feature, Issues = issues, CanContinue = true };

            return new FeatureStreamItem
            {
                Issues = issues.Count > 0 ? issues : [new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Error,
                    Code = "feature_parse_null",
                    Message = "FeatureParser returned null without reporting issues."
                }],
                CanContinue = true
            };
        }
        catch (Exception ex)
        {
            return new FeatureStreamItem
            {
                Exception = ex,
                Issues = issues,
                CanContinue = true
            };
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

        var hasLegacyGml = reader.NamespaceURI == GmlNamespaces.Gml;
        if (hasLegacyGml && LooksLikeGml2Indicator(reader.LocalName))
            return GmlVersion.V2_1_2;

        // Check namespace declarations
        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (reader.Value == GmlNamespaces.Gml33) return GmlVersion.V3_3;
                if (reader.Value == GmlNamespaces.Gml32) return GmlVersion.V3_2;
                if (reader.Value == GmlNamespaces.Gml) hasLegacyGml = true;
            } while (reader.MoveToNextAttribute());
            reader.MoveToElement();
        }

        return hasLegacyGml ? GmlVersion.V3_1 : GmlVersion.V3_2;
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

    /// <summary>Returns true if the local name suggests GML 2 content (lightweight check for streaming).</summary>
    private static bool LooksLikeGml2Indicator(string localName) =>
        localName is "coordinates" or "Box" or "outerBoundaryIs";
}

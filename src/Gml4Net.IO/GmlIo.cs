using System.Runtime.CompilerServices;
using System.Xml;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Ows;
using Gml4Net.Parser;
using Gml4Net.Parser.Streaming;
using System.Xml.Linq;

namespace Gml4Net.IO;

/// <summary>
/// File and HTTP I/O for parsing GML documents and streaming features.
/// </summary>
public static class GmlIo
{
    /// <summary>
    /// Parses a GML file synchronously.
    /// </summary>
    /// <param name="path">Path to the GML file.</param>
    /// <returns>The parse result.</returns>
    /// <exception cref="GmlIoException">Thrown for file access errors.</exception>
    public static GmlParseResult ParseFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        Stream stream;
        try
        {
            stream = File.OpenRead(path);
        }
        catch (FileNotFoundException ex)
        {
            throw new GmlIoException("file_not_found", $"File not found: {path}", ex);
        }
        catch (IOException ex)
        {
            throw new GmlIoException("file_read_error", $"Cannot read file: {path}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new GmlIoException("file_read_error", $"Access denied: {path}", ex);
        }

        using (stream)
        {
            return GmlParser.ParseStream(stream);
        }
    }

    /// <summary>
    /// Parses a GML file asynchronously.
    /// </summary>
    /// <param name="path">Path to the GML file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parse result.</returns>
    /// <exception cref="GmlIoException">Thrown for file access errors.</exception>
    public static async Task<GmlParseResult> ParseFileAsync(string path, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        }
        catch (FileNotFoundException ex)
        {
            throw new GmlIoException("file_not_found", $"File not found: {path}", ex);
        }
        catch (IOException ex)
        {
            throw new GmlIoException("file_read_error", $"Cannot read file: {path}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new GmlIoException("file_read_error", $"Access denied: {path}", ex);
        }

        return GmlParser.ParseBytes(bytes);
    }

    /// <summary>
    /// Parses a GML document from a URL via HTTP GET.
    /// Detects OWS ExceptionReport responses and converts them to parse issues.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="client">Optional HttpClient (a default is created if null).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parse result.</returns>
    /// <exception cref="GmlIoException">Thrown for HTTP or network errors.</exception>
    public static async Task<GmlParseResult> ParseUrlAsync(Uri url, HttpClient? client = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        var disposeClient = client is null;
        client ??= new HttpClient();

        try
        {
            using var response = await GetResponseAsync(client, url, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            EnsureSuccessStatusCode(response, url);

            var content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Check for OWS ExceptionReport (HTTP 200 with fachlicher Fehler)
            if (OwsExceptionParser.IsOwsExceptionReport(content))
            {
                var report = OwsExceptionParser.Parse(content);
                if (report is not null)
                    return CreateOwsIssueResult(report);
            }

            return GmlParser.ParseXmlString(content);
        }
        finally
        {
            if (disposeClient)
                client.Dispose();
        }
    }

    /// <summary>
    /// Streams features from a GML file.
    /// </summary>
    /// <param name="path">Path to the GML file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of features.</returns>
    /// <exception cref="GmlIoException">Thrown for file access errors.</exception>
    public static async IAsyncEnumerable<GmlFeature> StreamFeaturesFromFile(
        string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        Stream stream;
        try
        {
            stream = File.OpenRead(path);
        }
        catch (FileNotFoundException ex)
        {
            throw new GmlIoException("file_not_found", $"File not found: {path}", ex);
        }
        catch (IOException ex)
        {
            throw new GmlIoException("file_read_error", $"Cannot read file: {path}", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new GmlIoException("file_read_error", $"Access denied: {path}", ex);
        }

        await using (stream.ConfigureAwait(false))
        {
            await foreach (var feature in GmlFeatureStreamParser.ParseAsync(stream, ct).ConfigureAwait(false))
            {
                yield return feature;
            }
        }
    }

    /// <summary>
    /// Streams features from a URL via HTTP GET.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="client">Optional HttpClient (a default is created if null).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of features.</returns>
    /// <exception cref="GmlIoException">Thrown for HTTP or network errors.</exception>
    public static async IAsyncEnumerable<GmlFeature> StreamFeaturesFromUrl(
        Uri url,
        HttpClient? client = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        var disposeClient = client is null;
        client ??= new HttpClient();

        try
        {
            using var response = await GetResponseAsync(client, url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            EnsureSuccessStatusCode(response, url);

            var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                using var reader = GmlFeatureStreamParser.CreateReader(stream);
                if (await MoveToFirstElementAsync(reader).ConfigureAwait(false)
                    && IsOwsExceptionReport(reader))
                {
                    var reportElement = (XElement)await XNode.ReadFromAsync(reader, ct).ConfigureAwait(false);
                    var report = OwsExceptionParser.Parse(reportElement.ToString(SaveOptions.DisableFormatting));
                    if (report is not null)
                        throw CreateOwsException(report);
                }

                await foreach (var feature in GmlFeatureStreamParser.ParseAsync(reader, ct).ConfigureAwait(false))
                {
                    yield return feature;
                }
            }
        }
        finally
        {
            if (disposeClient)
                client.Dispose();
        }
    }

    /// <summary>Sends an HTTP GET request, wrapping HttpRequestException as GmlIoException.</summary>
    private static async Task<HttpResponseMessage> GetResponseAsync(
        HttpClient client,
        Uri url,
        HttpCompletionOption completionOption,
        CancellationToken ct)
    {
        try
        {
            return await client.GetAsync(url, completionOption, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GmlIoException("network_error", $"Network error fetching {url}: {ex.Message}", ex);
        }
    }

    /// <summary>Throws GmlIoException with http_error code if the response status is not 2xx.</summary>
    private static void EnsureSuccessStatusCode(HttpResponseMessage response, Uri url)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new GmlIoException("http_error",
                $"HTTP {(int)response.StatusCode} from {url}",
                httpStatusCode: (int)response.StatusCode);
        }
    }

    /// <summary>Converts an OWS ExceptionReport into a GmlParseResult with Error-severity issues.</summary>
    private static GmlParseResult CreateOwsIssueResult(OwsExceptionReport report)
    {
        var issues = report.Exceptions.Select(ex => new GmlParseIssue
        {
            Severity = GmlIssueSeverity.Error,
            Code = ex.ExceptionCode,
            Message = string.Join("; ", ex.ExceptionTexts),
            Location = ex.Locator
        }).ToList();

        return new GmlParseResult { Issues = issues };
    }

    /// <summary>Creates a GmlIoException with ows_exception error code from an OWS ExceptionReport.</summary>
    private static GmlIoException CreateOwsException(OwsExceptionReport report)
    {
        var first = report.Exceptions.FirstOrDefault();
        var code = first?.ExceptionCode ?? "ows_exception";
        var text = first is null
            ? "OWS ExceptionReport returned no exception details."
            : string.Join("; ", first.ExceptionTexts.Where(t => !string.IsNullOrWhiteSpace(t)));
        var message = string.IsNullOrWhiteSpace(text)
            ? $"OWS exception returned from service: {code}"
            : $"OWS exception returned from service: {code}: {text}";

        return new GmlIoException("ows_exception", message);
    }

    /// <summary>Advances the reader to the first Element node, returning false if none found.</summary>
    private static async Task<bool> MoveToFirstElementAsync(XmlReader reader)
    {
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (reader.NodeType == XmlNodeType.Element)
                return true;
        }

        return false;
    }

    /// <summary>Returns true if the reader is positioned on an OWS ExceptionReport root element.</summary>
    private static bool IsOwsExceptionReport(XmlReader reader) =>
        reader.LocalName == "ExceptionReport"
        && reader.NamespaceURI is GmlNamespaces.Ows or "http://www.opengis.net/ows/2.0";
}

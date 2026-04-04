using System.Net;
using System.Text;
using FluentAssertions;
using Gml4Net.IO;
using Gml4Net.Model;
using Gml4Net.Model.Feature;
using Gml4Net.Model.Geometry;
using Xunit;

namespace Gml4Net.IO.Tests;

public class GmlIoTests
{
    private static readonly string SampleGml = """
        <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                               xmlns:gml="http://www.opengis.net/gml/3.2"
                               xmlns:app="http://example.com/app">
            <wfs:member>
                <app:City gml:id="city.1">
                    <app:name>Munich</app:name>
                    <app:location>
                        <gml:Point><gml:pos>11.5 48.1</gml:pos></gml:Point>
                    </app:location>
                </app:City>
            </wfs:member>
        </wfs:FeatureCollection>
        """;

    // ---- ParseFile ----

    [Fact]
    public void ParseFile_WithValidFile_ReturnsResult()
    {
        var path = WriteTempFile(SampleGml);
        try
        {
            var result = GmlIo.ParseFile(path);

            result.HasErrors.Should().BeFalse();
            result.Document!.Root.Should().BeOfType<GmlFeatureCollection>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ParseFile_WithMissingFile_ThrowsGmlIoException()
    {
        var path = Path.Combine(Path.GetTempPath(), "gml4net_nonexistent_file.gml");
        var act = () => GmlIo.ParseFile(path);

        act.Should().Throw<GmlIoException>()
            .Which.ErrorCode.Should().Be("file_not_found");
    }

    // ---- ParseFileAsync ----

    [Fact]
    public async Task ParseFileAsync_WithValidFile_ReturnsResult()
    {
        var path = WriteTempFile(SampleGml);
        try
        {
            var result = await GmlIo.ParseFileAsync(path);

            result.HasErrors.Should().BeFalse();
            result.Document!.Root.Should().BeOfType<GmlFeatureCollection>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ParseFileAsync_WithMissingFile_ThrowsGmlIoException()
    {
        var path = Path.Combine(Path.GetTempPath(), "gml4net_nonexistent_file.gml");
        var act = () => GmlIo.ParseFileAsync(path);

        await act.Should().ThrowAsync<GmlIoException>()
            .Where(e => e.ErrorCode == "file_not_found");
    }

    // ---- ParseUrlAsync ----

    [Fact]
    public async Task ParseUrlAsync_WithSuccessResponse_ReturnsResult()
    {
        var handler = new MockHttpHandler(SampleGml, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        var result = await GmlIo.ParseUrlAsync(new Uri("https://example.com/wfs"), client);

        result.HasErrors.Should().BeFalse();
        result.Document!.Root.Should().BeOfType<GmlFeatureCollection>();
    }

    [Fact]
    public async Task ParseUrlAsync_WithHttpError_ThrowsGmlIoException()
    {
        var handler = new MockHttpHandler("Not Found", HttpStatusCode.NotFound);
        var client = new HttpClient(handler);

        var act = () => GmlIo.ParseUrlAsync(new Uri("https://example.com/wfs"), client);

        await act.Should().ThrowAsync<GmlIoException>()
            .Where(e => e.ErrorCode == "http_error" && e.HttpStatusCode == 404);
    }

    [Fact]
    public async Task ParseUrlAsync_WithOwsExceptionReport_ReturnsIssues()
    {
        var owsXml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
                <ows:Exception exceptionCode="NoSuchCoverage">
                    <ows:ExceptionText>Coverage not found</ows:ExceptionText>
                </ows:Exception>
            </ows:ExceptionReport>
            """;
        var handler = new MockHttpHandler(owsXml, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        var result = await GmlIo.ParseUrlAsync(new Uri("https://example.com/wcs"), client);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Code == "NoSuchCoverage");
        result.Document.Should().BeNull();
    }

    // ---- StreamFeaturesFromFile ----

    [Fact]
    public async Task StreamFeaturesFromFile_WithValidFile_StreamsFeatures()
    {
        var path = WriteTempFile(SampleGml);
        try
        {
            var features = new List<GmlFeature>();
            await foreach (var f in GmlIo.StreamFeaturesFromFile(path))
                features.Add(f);

            features.Should().HaveCount(1);
            features[0].Id.Should().Be("city.1");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StreamFeaturesFromFile_WithMissingFile_ThrowsGmlIoException()
    {
        var path = Path.Combine(Path.GetTempPath(), "gml4net_nonexistent_stream.gml");
        var act = async () =>
        {
            await foreach (var _ in GmlIo.StreamFeaturesFromFile(path)) { }
        };

        await act.Should().ThrowAsync<GmlIoException>()
            .Where(e => e.ErrorCode == "file_not_found");
    }

    // ---- StreamFeaturesFromUrl ----

    [Fact]
    public async Task StreamFeaturesFromUrl_WithSuccessResponse_StreamsFeatures()
    {
        var handler = new MockHttpHandler(SampleGml, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        var features = new List<GmlFeature>();
        await foreach (var f in GmlIo.StreamFeaturesFromUrl(new Uri("https://example.com/wfs"), client))
            features.Add(f);

        features.Should().HaveCount(1);
    }

    [Fact]
    public async Task StreamFeaturesFromUrl_WithHttpError_ThrowsGmlIoException()
    {
        var handler = new MockHttpHandler("Error", HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler);

        var act = async () =>
        {
            await foreach (var _ in GmlIo.StreamFeaturesFromUrl(new Uri("https://example.com/wfs"), client)) { }
        };

        await act.Should().ThrowAsync<GmlIoException>()
            .Where(e => e.ErrorCode == "http_error" && e.HttpStatusCode == 500);
    }

    [Fact]
    public async Task StreamFeaturesFromUrl_WithOwsExceptionReport_ThrowsGmlIoException()
    {
        var owsXml = """
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/2.0" version="2.0.1">
                <ows:Exception exceptionCode="InvalidParameterValue" locator="coverageId">
                    <ows:ExceptionText>Coverage not found</ows:ExceptionText>
                </ows:Exception>
            </ows:ExceptionReport>
            """;
        var handler = new MockHttpHandler(owsXml, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        var act = async () =>
        {
            await foreach (var _ in GmlIo.StreamFeaturesFromUrl(new Uri("https://example.com/wcs"), client)) { }
        };

        await act.Should().ThrowAsync<GmlIoException>()
            .Where(e => e.ErrorCode == "ows_exception" && e.Message.Contains("InvalidParameterValue"));
    }

    [Fact]
    public async Task ParseUrlAsync_DisposesResponseContent()
    {
        var content = new TrackingStringContent(SampleGml);
        var handler = new MockHttpHandler(content, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        await GmlIo.ParseUrlAsync(new Uri("https://example.com/wfs"), client);

        content.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task StreamFeaturesFromUrl_DisposesResponseContent_WhenEnumerationStopsEarly()
    {
        var content = new TrackingStringContent("""
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:app="http://example.com/app">
                <wfs:member><app:City gml:id="city.1"><app:name>Munich</app:name></app:City></wfs:member>
                <wfs:member><app:City gml:id="city.2"><app:name>Berlin</app:name></app:City></wfs:member>
            </wfs:FeatureCollection>
            """);
        var handler = new MockHttpHandler(content, HttpStatusCode.OK);
        var client = new HttpClient(handler);

        await foreach (var _ in GmlIo.StreamFeaturesFromUrl(new Uri("https://example.com/wfs"), client))
        {
            break;
        }

        content.IsDisposed.Should().BeTrue();
    }

    // ---- Helpers ----

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName() + ".gml";
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpContent _content;
        private readonly HttpStatusCode _statusCode;

        public MockHttpHandler(string responseBody, HttpStatusCode statusCode)
            : this(new StringContent(responseBody, Encoding.UTF8, "application/xml"), statusCode)
        {
        }

        public MockHttpHandler(HttpContent content, HttpStatusCode statusCode)
        {
            _content = content;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = _content
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TrackingStringContent : StringContent
    {
        public TrackingStringContent(string content)
            : base(content, Encoding.UTF8, "application/xml")
        {
        }

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}

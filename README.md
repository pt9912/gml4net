# Gml4Net

A .NET library for parsing Geography Markup Language (GML) documents. Supports GML 2-3.3, GeoJSON/WKT/KML/CSV export, OWS/WCS integration, and streaming for large documents.

## Features

- **GML Parsing** -- Geometries, features, feature collections, and coverages
- **Version Support** -- GML 2.1.2, 3.0, 3.1, 3.2, 3.3 with automatic version detection
- **GeoJSON Export** -- Conversion to GeoJSON via `System.Text.Json`
- **WKT Export** -- Conversion to Well-Known Text
- **KML Export** -- Conversion to KML via `System.Xml.Linq`
- **CSV Export** -- Feature collections to CSV with WKT geometry column
- **Generic Builder** -- `IBuilder<TGeometry, TFeature, TCollection>` for custom output formats
- **Coverage Support** -- RectifiedGridCoverage, GridCoverage, ReferenceableGridCoverage, MultiPointCoverage
- **WCS Integration** -- Request builder and capabilities parser for OGC Web Coverage Service
- **OWS Exceptions** -- Detection and parsing of OGC Web Service exception reports
- **Streaming** -- Memory-efficient processing of large WFS feature collections with callback-based public API, batch support, and feature sinks
- **Zero Dependencies** -- Only BCL APIs (`System.Xml.Linq`, `System.Text.Json`)

## Packages

| Package | Description |
|---------|-------------|
| `Gml4Net` | Core library: model, parser, GeoJSON/WKT/KML/CSV interop, OWS, WCS, streaming |
| `Gml4Net.IO` | Optional package: file and HTTP I/O, streaming from files and URLs |

## API

### Parse GML

```csharp
using Gml4Net.Parser;

var result = GmlParser.ParseXmlString(xml);

if (!result.HasErrors)
{
    var doc = result.Document!;
    // doc.Root is GmlPoint, GmlFeature, GmlFeatureCollection, or GmlCoverage
}
```

Also available: `GmlParser.ParseBytes(ReadOnlySpan<byte>)` and `GmlParser.ParseStream(Stream)`.

### Convert to GeoJSON

```csharp
using Gml4Net.Interop;

var geojson = GeoJsonBuilder.Document(result.Document!);
var jsonString = GeoJsonBuilder.GeometryToJson(point);
```

Or via the generic builder pattern:

```csharp
var parser = GmlParser.Create(GeoJsonBuilder.Instance);
var result = parser.Parse(xml);
// result.Geometry, result.Feature, result.Collection are JsonObject
```

### Convert to WKT

```csharp
using Gml4Net.Interop;

var wkt = WktBuilder.Geometry(point);
// POINT (10 20)
```

### Convert to KML

```csharp
using Gml4Net.Interop;

var kml = KmlBuilder.Feature(feature);
var kmlString = KmlBuilder.GeometryToKml(point);
```

### Pattern Matching on Root Content

```csharp
var description = result.Document!.Root switch
{
    GmlFeatureCollection fc => $"{fc.Features.Count} features",
    GmlFeature f            => $"Feature: {f.Id}",
    GmlGeometry g           => $"Geometry: {g.GetType().Name}",
    GmlCoverage c           => $"Coverage: {c.Id}",
    _                       => "Unknown"
};
```

### Stream Large Feature Collections

Callback-based streaming with error handling:

```csharp
using Gml4Net.Parser;

var parser = new StreamingGmlParser(new StreamingParserOptions
{
    ErrorBehavior = StreamingErrorBehavior.Continue
});

parser.OnFeature(feature =>
{
    Console.WriteLine(feature.Id);
    return ValueTask.CompletedTask;
});

parser.OnError(error =>
{
    Console.Error.WriteLine(error.Exception?.Message ?? "Parse issue");
});

var result = await parser.ParseAsync(stream);
// result.FeaturesProcessed, result.FeaturesFailed
```

With builder integration:

```csharp
var result = await StreamingGml.ParseAsync(
    stream,
    GeoJsonBuilder.Instance,
    feature =>
    {
        Console.WriteLine(feature["id"]);
        return ValueTask.CompletedTask;
    });
```

Batch processing:

```csharp
var result = await StreamingGml.ParseBatchesAsync(
    stream,
    GeoJsonBuilder.Instance,
    batch => SaveBatchAsync(batch),
    batchSize: 100);
```

With a feature sink (e.g. for database writes):

```csharp
var result = await StreamingGml.ParseAsync(stream, new PostGisSink(connection));
```

Low-level streaming via `IAsyncEnumerable<GmlFeature>`:

```csharp
using Gml4Net.Parser.Streaming;

await foreach (var feature in GmlFeatureStreamParser.ParseAsync(stream))
{
    Console.WriteLine(feature.Id);
}
```

### File and URL I/O

```csharp
using Gml4Net.IO;

// Parse from file
var fileResult = GmlIo.ParseFile("data.gml");

// Parse from URL
var urlResult = await GmlIo.ParseUrlAsync(uri);

// Stream features from URL
await foreach (var feature in GmlIo.StreamFeaturesFromUrl(uri))
{
    Console.WriteLine(feature.Id);
}
```

## Supported GML Types

### Geometries

| Type | GML 2 | GML 3 |
|------|-------|-------|
| Point | x | x |
| LineString | x | x |
| LinearRing | x | x |
| Polygon | x | x |
| Envelope | - | x |
| Box | x | - |
| Curve | - | x |
| Surface | - | x |
| MultiPoint | x | x |
| MultiLineString | x | x |
| MultiPolygon | x | x |

### Features

- `GmlFeature` with typed properties (string, numeric, geometry, nested, raw XML)
- `GmlFeatureCollection` with WFS 1.0/1.1/2.0 member variants

### Coverages

- `GmlRectifiedGridCoverage` (georeferenced grids with affine transform)
- `GmlGridCoverage` (non-georeferenced grids)
- `GmlReferenceableGridCoverage` (irregular grids)
- `GmlMultiPointCoverage` (discrete point coverages)

## Error Handling

Parse errors are returned as issues, not thrown as exceptions:

```csharp
var result = GmlParser.ParseXmlString(xml);

if (result.HasErrors)
{
    foreach (var issue in result.Issues)
    {
        Console.WriteLine($"[{issue.Severity}] {issue.Code}: {issue.Message}");
    }
}
```

## Docker Build and Release

The repository uses a multi-stage [Dockerfile](Dockerfile) for building, testing, and packaging.

- Build stages: `restore`, `build`, `test`, `pack`, `push`
- Coverage gate: at least 90% line coverage
- Public API XML documentation comments are required (`CS1591` as error)

```bash
docker buildx build --target test -t gml4net:test .
docker buildx build --target artifacts --build-arg PACKAGE_VERSION=0.1.0 -o type=local,dest=./artifacts .
docker buildx build --target push --secret id=nuget_api_key,src=/path/to/nuget-api-key.txt --build-arg PACKAGE_VERSION=0.1.0 .
```

Notes:

- `push` publishes generated `.nupkg` files to `https://api.nuget.org/v3/index.json`
- The preferred credential flow is a BuildKit secret named `nuget_api_key`
- `NUGET_API_KEY` is also supported as a build argument for CI systems that cannot mount secrets
- The example commands use `docker buildx build` because the Dockerfile relies on BuildKit features
- The `test` stage enforces `/p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total`
- The current test project uses `coverlet.msbuild`; the 90% gate is enforced via the Docker test stage
- The library project treats `CS1591` as an error, so missing XML comments on public APIs break the Docker build

## GitHub Actions

The repository includes GitHub Actions workflows that use the same Dockerfile-based pipeline as local development:

- `.github/workflows/ci.yml`
  - runs on pushes to `main` and on pull requests
  - executes `docker buildx build --target test -t gml4net:test .`
- `.github/workflows/publish-gml4net.yml`
  - publishes the `Gml4Net` package
  - runs automatically on Git tags matching `Gml4Net-v*`
  - can also be started manually via `workflow_dispatch`
- `.github/workflows/publish-gml4net-io.yml`
  - publishes the `Gml4Net.IO` package
  - runs automatically on Git tags matching `Gml4Net.IO-v*`
  - can also be started manually via `workflow_dispatch`
- both publish workflows validate the package version, run the Docker `test` target, build package-specific NuGet artifacts, upload them as workflow artifacts, and publish to `nuget.org`

Required GitHub repository setup:

- create a repository or environment secret named `NUGET_API_KEY`
- if you use GitHub environments, the publish workflow targets the `nuget` environment
- publish tags must use the format `<PackageId>-v<semver>`
- examples:
  - `Gml4Net-v0.2.0`
  - `Gml4Net.IO-v0.2.0`

Example release flow:

```bash
git tag Gml4Net-v0.2.0
git push origin Gml4Net-v0.2.0

git tag Gml4Net.IO-v0.2.0
git push origin Gml4Net.IO-v0.2.0
```

## Requirements

- Target: .NET 10.0 (LTS)

## Documentation

- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Porting Design](docs/port2net.md)
- [Streaming Parser](docs/streaming-parser.md)
- [Releasing](docs/releasing.md)

## Related Projects

- [s-gml](https://github.com/pt9912/s-gml) -- Original TypeScript implementation
- [gml4dart](https://github.com/pt9912/gml4dart) -- Dart port

## License

[MIT](LICENSE)

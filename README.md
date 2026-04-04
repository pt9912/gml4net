# Gml4Net

Design and planning documents for a proposed .NET library for parsing Geography Markup Language (GML) documents.

## Status

Phase 1 (Core-Modell + Geometrie-Parser) is complete.

- Geometry parsing works for all GML 2 and GML 3 types (41 tests passing)
- No NuGet packages have been published yet
- Feature parsing (Phase 2) is next

## Planned Scope

- **GML Parsing** -- Planned support for geometries, features, feature collections, and coverages
- **Version Support** -- Planned support for GML 2.1.2, 3.0, 3.1, 3.2, 3.3 with automatic version detection
- **GeoJSON Export** -- Planned conversion to GeoJSON via `System.Text.Json`
- **WKT Export** -- Planned conversion to Well-Known Text
- **Coverage Support** -- Planned coverage support for RectifiedGridCoverage, GridCoverage, ReferenceableGridCoverage, MultiPointCoverage
- **WCS Integration** -- Planned request builder and capabilities parser for OGC Web Coverage Service
- **OWS Exceptions** -- Planned detection and parsing of OGC Web Service exception reports
- **Streaming** -- Planned memory-efficient processing of large WFS feature collections via `IAsyncEnumerable<T>`
- **Zero Dependencies** -- The core design targets only BCL APIs (`System.Xml.Linq`, `System.Text.Json`)

## Planned Packages

- `Gml4Net` -- planned core package
- `Gml4Net.IO` -- planned optional I/O package for file and HTTP access

## Planned API Sketch

The following snippets describe the intended public API. They are design targets, not runnable examples against a published package.

### Parse GML

```csharp
using Gml4Net;

var xml = """
    <gml:Point xmlns:gml="http://www.opengis.net/gml/3.2">
        <gml:pos>10.0 20.0</gml:pos>
    </gml:Point>
    """;

var result = GmlParser.ParseXmlString(xml);

if (!result.HasErrors)
{
    var doc = result.Document!;
    // doc.Root is GmlPoint, GmlFeature, GmlFeatureCollection, or GmlCoverage
}
```

### Convert to GeoJSON

```csharp
using Gml4Net.Interop;

var point = (GmlGeometry)result.Document!.Root;
var geojson = GeoJsonBuilder.Document(result.Document!);
// {"type": "Point", "coordinates": [10.0, 20.0]}

var jsonString = GeoJsonBuilder.GeometryToJson(point);
```

### Convert to WKT

```csharp
using Gml4Net.Interop;

var point = (GmlGeometry)result.Document!.Root;
var wkt = WktBuilder.Geometry(point);
// POINT (10 20)
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
var uri = new Uri("https://example.com/wfs?...");
var urlResult = await GmlIo.ParseUrlAsync(uri);

// Stream features from URL
await foreach (var feature in GmlIo.StreamFeaturesFromUrl(uri))
{
    Console.WriteLine(feature.Id);
}
```

## Planned Supported GML Types

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

## Planned Package Split

| Package | Description |
|---------|-------------|
| `Gml4Net` | Planned core library: model, parser, GeoJSON/WKT interop, OWS, WCS |
| `Gml4Net.IO` | Planned optional package: file and HTTP I/O, streaming from files and URLs |

## Docker Build and Release

The repository now includes a multi-stage [Dockerfile](Dockerfile) for the planned .NET solution layout.

- Expected solution path by default: `GML4Net.sln`
- Build stages: `restore`, `build`, `test`, `pack`, `push`
- Package output stage: `artifacts`
- Release target: `nuget.org`

Example commands once the solution and projects exist:

```bash
docker buildx build --target test -t gml4net:test .
docker buildx build --target pack --build-arg PACKAGE_VERSION=0.1.0 -o type=local,dest=./artifacts .
docker buildx build --target push --secret id=nuget_api_key,src=/path/to/nuget-api-key.txt --build-arg PACKAGE_VERSION=0.1.0 .
```

Notes:

- `push` publishes generated `.nupkg` files to `https://api.nuget.org/v3/index.json`
- The preferred credential flow is a BuildKit secret named `nuget_api_key`
- `NUGET_API_KEY` is also supported as a build argument for CI systems that cannot mount secrets
- The example commands use `docker buildx build` because the Dockerfile relies on BuildKit features
- The Dockerfile is a scaffold for the design-phase project and will become executable once `GML4Net.sln` and the referenced projects exist

## Requirements

- Target: .NET 10.0 (LTS)

## Documentation

- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Porting Design](docs/port2net.md)

## Related Projects

- [s-gml](https://github.com/niclas9912/s-gml) -- Original TypeScript implementation
- [gml4dart](https://github.com/niclas9912/gml4dart) -- Dart port

## License

[MIT](LICENSE)

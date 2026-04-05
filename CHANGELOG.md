# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2] - 2026-04-04

### Fixed

- Add NuGet package metadata: Authors, Copyright, PackageProjectUrl, RepositoryUrl, RepositoryType
- Expand Description with feature highlights for better nuget.org discoverability
- Add additional PackageTags (kml, wcs, wfs, parsing, streaming)

## [0.1.1] - 2026-04-04

### Fixed

- Embed README.md in NuGet packages so nuget.org displays the project description
- Fix local pack commands in docs to use `--target artifacts` instead of `--target pack`

## [0.1.0] - 2026-04-04

Initial release covering all seven implementation phases.

### Added

#### Core Model (Phase 1)

- Domain model with sealed type hierarchies: `GmlNode`, `GmlDocument`, `GmlVersion`, `GmlCoordinate`
- 11 geometry types: Point, LineString, LinearRing, Polygon, Envelope, Box, Curve, Surface, MultiPoint, MultiLineString, MultiPolygon
- Feature model: `GmlFeature`, `GmlFeatureCollection`, `GmlPropertyBag` with ordered property entries
- Coverage model: `GmlCoverage` hierarchy (RectifiedGrid, Grid, Referenceable, MultiPoint)
- Parse result model: `GmlParseResult`, `GmlParseIssue` (no exceptions for parse errors)
- `GmlParser` public API: `ParseXmlString`, `ParseBytes`, `ParseStream`
- GML version detection (2.1.2, 3.0, 3.1, 3.2, 3.3) via namespace and content heuristics

#### Feature Parser (Phase 2)

- `FeatureParser` with `ParseCollection` and `ParseFeature`
- All WFS member variants: `gml:featureMember`, `wfs:member`, `gml:featureMembers`
- Property value dispatch: String, Numeric, Geometry, Nested, RawXml
- Standalone feature detection heuristic

#### Coverage Parser and Generator (Phase 3)

- `CoverageParser` for RectifiedGridCoverage, GridCoverage, ReferenceableGridCoverage, MultiPointCoverage
- `CoverageGenerator` producing GML 3.2 / gmlcov XML with namespace declarations
- `GeoTiffUtils` for raster metadata extraction, PixelToWorld / WorldToPixel transforms
- Both GML and GMLCOV namespace support

#### Interop Builders (Phase 4 + 7)

- `GeoJsonBuilder` -- GeoJSON via System.Text.Json (Geometry, Feature, FeatureCollection, Document)
- `WktBuilder` -- Well-Known Text with 2D/3D/M/ZM support and EMPTY handling
- `KmlBuilder` -- KML 2.2 output (Point, LineString, Polygon, MultiGeometry, Placemark, Document)
- `CsvBuilder` -- CSV with WKT geometry column, RFC 4180 compliant (CRLF, escaping)
- `IGmlBuilder<TGeometry, TFeature, TCollection>` interface implemented by all three builders
- All builders support M-coordinates; KML documents omission per KML 2.2

#### OWS + WCS (Phase 5)

- `OwsExceptionParser` -- detection and parsing of OWS 1.1 and 2.0 ExceptionReport
- `WcsRequestBuilder` -- version-aware GetCoverage URL and XML POST builder (WCS 1.0-2.0)
- `WcsCapabilitiesParser` -- WCS 2.0 and 1.0 legacy GetCapabilities parsing

#### Streaming + I/O (Phase 6)

- `GmlFeatureStreamParser` -- XmlReader-based forward-only streaming with `IAsyncEnumerable<GmlFeature>`
- `ProcessFeaturesAsync` callback variant
- O(1) memory for featureMember, wfs:member, and featureMembers (plural) paths
- CancellationToken support with cooperative cancellation
- **Gml4Net.IO package:**
  - `GmlIo.ParseFile` / `ParseFileAsync` / `ParseUrlAsync`
  - `GmlIo.StreamFeaturesFromFile` / `StreamFeaturesFromUrl`
  - `GmlIoException` with error codes: file_not_found, file_read_error, http_error, network_error, ows_exception
  - OWS ExceptionReport detection in both DOM and streaming HTTP paths

### Infrastructure

- .NET 10 / C# 14 target framework
- Multi-stage Dockerfile (restore, build, test, pack, push, artifacts)
- 90% line coverage gate enforced via coverlet in Docker test stage
- CS1591 (missing XML doc comments) enforced as build error
- GitHub Actions CI workflow + per-package publish workflows
- xUnit v3 + FluentAssertions test framework

### Documentation

- Architecture document (`docs/architecture.md`)
- Implementation roadmap (`docs/roadmap.md`)
- Porting design (`docs/port2net.md`)
- Streaming parser design (`docs/streaming-parser.md`)
- Release process (`docs/releasing.md`)

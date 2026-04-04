# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial design-phase documentation
- Porting design document (`docs/port2net.md`)
- Architecture document (`docs/architecture.md`)
- Implementation roadmap (`docs/roadmap.md`)
- Phase 1 implementation: core domain model and geometry parser
  - Solution structure (`GML4Net.sln`, `Directory.Build.props`, `Dockerfile`) targeting .NET 10 / C# 14
  - Domain model: `GmlNode`, `GmlDocument`, `GmlVersion`, `GmlCoordinate`, `GmlParseResult`, `GmlParseIssue`
  - 11 geometry types: Point, LineString, LinearRing, Polygon, Envelope, Box, Curve, Surface, MultiPoint, MultiLineString, MultiPolygon
  - Feature model types: `GmlFeature`, `GmlFeatureCollection`, `GmlPropertyValue` hierarchy
  - Coverage model types: `GmlCoverage` hierarchy, `GmlGrid`, `GmlRectifiedGrid`, `GmlRangeSet`, `GmlRangeType`
  - Parser: `GmlParser` (public API), `GeometryParser`, `XmlHelpers`, `GmlNamespaces`
  - GML version detection (2.1.2, 3.0, 3.1, 3.2, 3.3) via namespace and content heuristics
  - Support for MultiCurve, MultiSurface, MultiGeometry dispatch
  - 41 unit tests (xUnit v3 + FluentAssertions)

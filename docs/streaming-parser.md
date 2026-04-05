# Streaming-Pfad fuer grosse GML-Dokumente

Status: Noch nicht implementiert.

## Ausgangslage

`GmlParser<TGeometry, TFeature, TCollection>` ist der Standard-Parser fuer
GML-Dokumente. Er laed das komplette Dokument in den Speicher (DOM-basiert):

```csharp
var parser = GmlParser.Create(GeoJsonBuilder.Instance);
var result = parser.Parse(xml);
```

Das ist fuer normale Dokumentgroessen sinnvoll. Fuer sehr grosse
WFS-/GML-Dokumente (Multi-GB, Tausende Features) reicht der DOM-Pfad
nicht — die komplette FeatureCollection wird materialisiert.

## Vorbild: s-gml

In s-gml gibt es zwei getrennte Klassen:

- `GmlParser` — Standard-Pfad, alles im Speicher
- `StreamingGmlParser` — eigene Klasse, event-basiert, Chunk-Verarbeitung

```typescript
const streamParser = new StreamingGmlParser({
    builder: new GeoJsonBuilder(),
    batchSize: 100
});

streamParser.on('feature', (feature) => { /* pro Feature */ });
streamParser.on('error', (error) => { /* Fehler pro Feature */ });
await streamParser.parseStream(readableStream);
// Ergebnis: Anzahl verarbeiteter Features
```

Drei Events:

| s-gml Event | Bedeutung | gml4net Aequivalent |
|---|---|---|
| `on('feature', ...)` | Feature verarbeitet | `IBuilder.BuildFeature()` |
| `on('error', ...)` | Fehler bei einem Feature | `StreamingOptions.OnError` |
| `on('end', ...)` | Verarbeitung abgeschlossen | `StreamingResult` Rueckgabewert |

## Design

### StreamingGmlParser<TGeometry, TFeature, TCollection>

```csharp
namespace Gml4Net.Parser;

/// <summary>
/// Streaming GML parser for large FeatureCollection documents.
/// Parses features one at a time via XmlReader (forward-only, constant memory)
/// and converts each feature immediately through the builder.
/// </summary>
public class StreamingGmlParser<TGeometry, TFeature, TCollection>
{
    private readonly IBuilder<TGeometry, TFeature, TCollection> _builder;
    private readonly StreamingOptions _options;

    public StreamingGmlParser(
        IBuilder<TGeometry, TFeature, TCollection> builder,
        StreamingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        _builder = builder;
        _options = options ?? new StreamingOptions();
    }

    /// <summary>
    /// Parses features incrementally from a stream. Each feature is parsed
    /// via XmlReader, converted through the builder's BuildFeature method,
    /// and then discarded. The complete FeatureCollection is never materialized.
    /// </summary>
    /// <param name="stream">A stream containing a GML FeatureCollection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the number of features processed, skipped, and any issues.</returns>
    public async Task<StreamingResult> ParseAsync(
        Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var processed = 0;
        var skipped = 0;
        var issues = new List<GmlParseIssue>();

        await foreach (var gmlFeature in GmlFeatureStreamParser.ParseAsync(stream, ct))
        {
            try
            {
                _builder.BuildFeature(gmlFeature);
                processed++;
            }
            catch (Exception ex)
            {
                if (_options.OnError == StreamErrorBehavior.StopOnFirstError)
                    throw;

                skipped++;
                issues.Add(new GmlParseIssue
                {
                    Severity = GmlIssueSeverity.Warning,
                    Code = "feature_skipped",
                    Message = $"Feature '{gmlFeature.Id}' skipped: {ex.Message}",
                    Location = gmlFeature.Id
                });
            }

            _options.Progress?.Report(new StreamingProgress(processed, skipped));
        }

        return new StreamingResult
        {
            FeaturesProcessed = processed,
            FeaturesSkipped = skipped,
            Issues = issues
        };
    }
}
```

### StreamingOptions

```csharp
namespace Gml4Net.Parser;

/// <summary>
/// Configuration for the streaming parser.
/// </summary>
public sealed class StreamingOptions
{
    /// <summary>
    /// Behavior when a feature fails to build.
    /// Default: stop on first error.
    /// </summary>
    public StreamErrorBehavior OnError { get; init; } = StreamErrorBehavior.StopOnFirstError;

    /// <summary>
    /// Optional progress reporting. Reports after each feature.
    /// </summary>
    public IProgress<StreamingProgress>? Progress { get; init; }
}

public enum StreamErrorBehavior
{
    /// <summary>Rethrow the exception — abort processing.</summary>
    StopOnFirstError,

    /// <summary>Skip the feature, record a warning, continue.</summary>
    SkipMalformedFeature
}
```

### StreamingResult

```csharp
namespace Gml4Net.Parser;

/// <summary>
/// Result of a streaming parse operation.
/// </summary>
public sealed class StreamingResult
{
    /// <summary>Number of features successfully processed by the builder.</summary>
    public int FeaturesProcessed { get; init; }

    /// <summary>Number of features skipped due to errors.</summary>
    public int FeaturesSkipped { get; init; }

    /// <summary>Diagnostic issues encountered during processing.</summary>
    public IReadOnlyList<GmlParseIssue> Issues { get; init; } = [];
}
```

### StreamingProgress

```csharp
namespace Gml4Net.Parser;

/// <summary>
/// Progress snapshot reported during streaming.
/// </summary>
public readonly record struct StreamingProgress(int FeaturesProcessed, int FeaturesSkipped);
```

### Factory-Methode

Auf dem bestehenden statischen `GmlParser`:

```csharp
public static class GmlParser
{
    // Standard-Parser (bereits implementiert)
    public static GmlParser<TGeometry, TFeature, TCollection>
        Create<TGeometry, TFeature, TCollection>(
            IBuilder<TGeometry, TFeature, TCollection> builder)
        => new(builder);

    // Streaming-Parser
    public static StreamingGmlParser<TGeometry, TFeature, TCollection>
        CreateStreaming<TGeometry, TFeature, TCollection>(
            IBuilder<TGeometry, TFeature, TCollection> builder)
        => new(builder);
}
```

## Internes Zusammenspiel

```
Stream (XML)
  │
  ▼
GmlFeatureStreamParser (bestehend, XmlReader-basiert)
  │  — liest Feature-Elemente einzeln via XmlReader (forward-only)
  │  — parsed jedes Feature-Fragment ueber FeatureParser → GmlFeature
  │  — liefert GmlFeature per IAsyncEnumerable
  │
  ▼
StreamingGmlParser<,,> (neu)
  │  — empfaengt GmlFeature
  │  — ruft _builder.BuildFeature(gmlFeature) auf
  │  — bei Fehler: abbrechen oder ueberspringen (StreamErrorBehavior)
  │  — meldet Fortschritt ueber IProgress<StreamingProgress>
  │  — zaehlt processed/skipped
  │
  ▼
IBuilder.BuildFeature() (z.B. PostGisBuilder, GeoJsonFileBuilder, ...)
  │
  ▼
StreamingResult { FeaturesProcessed, FeaturesSkipped, Issues }
```

## Nutzung

### Einfach (alle Defaults)

```csharp
var streamParser = GmlParser.CreateStreaming(new PostGisBuilder(connection));
var result = await streamParser.ParseAsync(input);
// result.FeaturesProcessed == 42000
```

### Mit Fehlertoleranz

```csharp
var streamParser = new StreamingGmlParser<...>(
    new PostGisBuilder(connection),
    new StreamingOptions { OnError = StreamErrorBehavior.SkipMalformedFeature });

var result = await streamParser.ParseAsync(input);
// result.FeaturesProcessed == 41998
// result.FeaturesSkipped == 2
// result.Issues enthaelt die 2 Warnungen
```

### Mit Fortschrittsanzeige

```csharp
var progress = new Progress<StreamingProgress>(p =>
    Console.WriteLine($"Verarbeitet: {p.FeaturesProcessed}"));

var streamParser = new StreamingGmlParser<...>(
    new PostGisBuilder(connection),
    new StreamingOptions { Progress = progress });

await streamParser.ParseAsync(input);
```

## Vergleich

| Aspekt | GmlParser<,,> | StreamingGmlParser<,,> |
|---|---|---|
| Erzeugen | `GmlParser.Create(builder)` | `GmlParser.CreateStreaming(builder)` |
| Eingabe | string, byte[], Stream | Stream |
| Verarbeitung | DOM (komplett im Speicher) | Forward-Only (pro Feature) |
| Rueckgabe | `GmlBuildResult` | `StreamingResult` |
| Builder | `IBuilder` | `IBuilder` (gleich) |
| Root-Typen | Geometry, Feature, Collection, Coverage | nur FeatureCollection |
| Speicher | proportional zur Dokumentgroesse | konstant pro Feature |
| Fehler | `GmlParseIssue` in Result | `StreamErrorBehavior` (stop/skip) |
| Fortschritt | — | `IProgress<StreamingProgress>` |
| Basis | `GmlParser.ParseXmlString()` etc. | `GmlFeatureStreamParser.ParseAsync()` |

## Dateien

| Datei | Aenderung |
|---|---|
| `src/Gml4Net/Parser/StreamingGmlParser.cs` | **Neu** — Generische Klasse |
| `src/Gml4Net/Parser/StreamingOptions.cs` | **Neu** — Options + Enums |
| `src/Gml4Net/Parser/StreamingResult.cs` | **Neu** — Ergebnis + Progress |
| `src/Gml4Net/Parser/GmlParser.cs` | `CreateStreaming()` Factory |
| `tests/Gml4Net.Tests/Parser/StreamingGmlParserTests.cs` | **Neu** — Tests |

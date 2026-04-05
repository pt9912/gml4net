# Feature-Filter fuer den Streaming-Pfad

Status: Entwurf.

## Motivation

Der Streaming-Pfad parst alle Features eines GML-/WFS-Dokuments. In der Praxis
werden haeufig nur Teilmengen benoetigt — z.B. Features innerhalb eines
Begrenzungsrahmens oder mit bestimmten Property-Werten. Ohne Filter muss der
Aufrufer die Selektion im Callback selbst implementieren, und die Zaehler in
`StreamingResult` spiegeln nicht wider, wie viele Features tatsaechlich relevant
waren.

## Designentscheidung

Der Filter wird als `Func<GmlFeature, bool>` auf `StreamingParserOptions`
platziert, nicht als separater Parameter auf den Convenience-Methoden.

Begruendung:

- wirkt einheitlich auf alle Streaming-Pfade (`StreamingGmlParser`,
  `StreamingGml.ParseAsync`, `ParseBatchesAsync`, Sink)
- haelt die ohnehin lange Parameterliste von `StreamingGml` kurz
- passt semantisch zu den anderen Parse-Optionen (`ErrorBehavior`, `Progress`)
- ein `Func<GmlFeature, bool>` ist maximal flexibel — der Aufrufer kann
  Property-Filter, BBox-Pruefungen oder beliebige Logik ausdruecken

## API

### StreamingParserOptions

Siehe `src/Gml4Net/Parser/Streaming/StreamingParserOptions.cs`.

Neues Property:

```csharp
/// <summary>
/// Optional predicate that determines whether a successfully parsed feature
/// should be emitted to the configured callback, batch, or sink.
/// Features for which the predicate returns false are silently skipped
/// and counted as filtered, not as processed or failed.
/// </summary>
public Func<GmlFeature, bool>? Filter { get; init; }
```

### StreamingResult

Siehe `src/Gml4Net/Parser/Streaming/StreamingResult.cs`.

Neues Property:

```csharp
/// <summary>
/// Number of features that were successfully parsed but excluded by the
/// configured filter predicate.
/// </summary>
public int FeaturesFiltered { get; init; }
```

### StreamingProgress

Siehe `src/Gml4Net/Parser/Streaming/StreamingResult.cs`.

Neuer Parameter:

```csharp
public readonly record struct StreamingProgress(
    int FeaturesProcessed,
    int FeaturesFailed,
    int FeaturesFiltered);
```

## Semantik

- der Filter wird nach erfolgreichem Parsen, aber *vor* dem Callback/Batch/Sink
  ausgewertet
- ein Feature, fuer das `Filter` `false` zurueckgibt, wird:
  - nicht an `OnFeature`, `onBatch` oder `IFeatureSink.WriteFeatureAsync`
    weitergereicht
  - nicht als `FeaturesProcessed` gezaehlt
  - nicht als `FeaturesFailed` gezaehlt
  - als `FeaturesFiltered` gezaehlt
- `FeaturesProcessed + FeaturesFailed + FeaturesFiltered` ergibt die Anzahl
  aller erfolgreich geparsten Features (ohne fatale Parse-Fehler)
- `Progress` meldet nach jedem Feature-Ergebnis (inkl. gefilterter Features)
  die kumulativen Zaehler
- wirft der Filter selbst eine Exception, wird das Feature als fehlgeschlagen
  behandelt (nicht als gefiltert), und `OnError` wird aufgerufen
- `OnEnd` erhaelt den `StreamingResult` mit allen drei Zaehlern
- wenn kein `Filter` gesetzt ist, aendert sich nichts am bisherigen Verhalten

## Auswertungsreihenfolge

```text
GmlFeatureStreamParser.ParseItemsAsync
  |
  v
FeatureStreamItem
  |
  +-- IsSuccess == false --> failed++, OnError
  |
  +-- IsSuccess == true
        |
        +-- Filter == null oder Filter(feature) == true
        |     |
        |     v
        |   OnFeature / onBatch / WriteFeatureAsync
        |     |
        |     +-- Erfolg --> processed++
        |     +-- Exception --> failed++, OnError
        |
        +-- Filter(feature) == false --> filtered++
        |
        +-- Filter(feature) wirft Exception --> failed++, OnError
```

## Im Batch-Pfad

- der Filter wird pro Feature *vor* dem Hinzufuegen zum Batch ausgewertet
- gefilterte Features landen nicht im Batch-Buffer
- der Batch-Zaehler (`batchSize`) zaehlt nur ungefilterte Features
- ein Batch mit 100 ungefilterten Features wird geflusht, auch wenn insgesamt
  500 Features geparst und 400 davon gefiltert wurden

## Nutzung

### Property-Filter

```csharp
var result = await StreamingGml.ParseAsync(
    stream,
    new GeoJsonBuilder(),
    feature => ProcessAsync(feature),
    options: new StreamingParserOptions
    {
        Filter = f => f.Properties["status"]?.ToString() == "active"
    });

// result.FeaturesProcessed -- nur aktive Features
// result.FeaturesFiltered  -- uebersprungene Features
```

### BBox-Clipping

```csharp
var bbox = new Envelope(minX: 11.0, minY: 47.0, maxX: 12.0, maxY: 48.0);

var result = await StreamingGml.ParseAsync(
    stream,
    new GeoJsonBuilder(),
    feature => ProcessAsync(feature),
    options: new StreamingParserOptions
    {
        Filter = f => HasGeometryInBBox(f, bbox)
    });

static bool HasGeometryInBBox(GmlFeature feature, Envelope bbox)
{
    foreach (var entry in feature.Properties)
    {
        if (entry.Value is GmlGeometryProperty gp
            && gp.Geometry is GmlPoint pt
            && pt.Coordinate.X >= bbox.MinX
            && pt.Coordinate.X <= bbox.MaxX
            && pt.Coordinate.Y >= bbox.MinY
            && pt.Coordinate.Y <= bbox.MaxY)
            return true;
    }
    return false;
}
```

### Kombiniert mit Fehlertoleranz

```csharp
var parser = new StreamingGmlParser(new StreamingParserOptions
{
    ErrorBehavior = StreamingErrorBehavior.Continue,
    Filter = f => f.Id?.StartsWith("building.") == true
});

parser.OnFeature(f => InsertAsync(f));
parser.OnError(e => Log.Warning(e.Exception, "Skipped"));

var result = await parser.ParseAsync(stream);
// result.FeaturesProcessed == 1200
// result.FeaturesFailed    == 3
// result.FeaturesFiltered  == 8500
```

### Batch mit Filter

```csharp
var result = await StreamingGml.ParseBatchesAsync(
    stream,
    new GeoJsonBuilder(),
    batch => BulkInsertAsync(batch),
    batchSize: 100,
    options: new StreamingParserOptions
    {
        Filter = f => f.Properties.ContainsKey("geometry")
    });

// Batches enthalten nur Features mit Geometrie
```

## Testfaelle

Mindestens diese Faelle muessen abgedeckt sein:

- Filter laesst alle Features durch → `FeaturesFiltered == 0`, Verhalten wie ohne Filter
- Filter schliesst alle Features aus → `FeaturesProcessed == 0`, `FeaturesFiltered == N`
- Filter schliesst einige Features aus → korrekte Aufteilung der Zaehler
- kein Filter gesetzt → `FeaturesFiltered == 0`, bisheriges Verhalten unveraendert
- Filter wirft Exception → Feature als fehlgeschlagen gezaehlt, nicht als gefiltert
- Filter + `ErrorBehavior.Stop` → Stop nach erstem Fehler, gefilterte Zaehler korrekt
- Filter + `ParseBatchesAsync` → Batch enthaelt nur ungefilterte Features
- Filter + `ParseBatchesAsync` → `batchSize` zaehlt ungefilterte Features
- Filter + `IFeatureSink` → `WriteFeatureAsync` nur fuer ungefilterte Features
- Filter + `IFeatureSink` → `CompleteAsync` wird trotz gefilterter Features aufgerufen
- Filter + `Progress` → meldet `FeaturesFiltered` nach jedem Ergebnis
- `StreamingResult` hat korrekte `FeaturesProcessed + FeaturesFailed + FeaturesFiltered`-Summe

## Ziel-Dateien

| Datei | Aenderung |
|---|---|
| `src/Gml4Net/Parser/Streaming/StreamingParserOptions.cs` | `Filter` Property |
| `src/Gml4Net/Parser/Streaming/StreamingResult.cs` | `FeaturesFiltered` auf Result und Progress |
| `src/Gml4Net/Parser/Streaming/StreamingGmlParser.cs` | Filter-Auswertung in `ParseAsync` |
| `src/Gml4Net/Parser/Streaming/StreamingGml.cs` | Filter-Auswertung in allen drei Convenience-Methoden |
| `tests/Gml4Net.Tests/Streaming/StreamingGmlParserTests.cs` | Filter-Tests |

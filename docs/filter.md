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
/// and counted as filtered, not as processed or failed. If the parsed feature
/// already carries non-fatal parser diagnostics, these diagnostics are still
/// forwarded via the configured error callback.
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
- traegt ein gefiltertes Feature bereits parsernahe `Issues`, werden diese trotz
  Filterung ueber den konfigurierten Fehlerkanal weitergegeben
  (`OnError(...)` bzw. `onError`), mit `Exception == null` und
  `CanContinue == true`
- diese Diagnostik-Weiterleitung ist rein informativ und unterliegt nicht dem
  `ErrorBehavior` — sie loest kein `Stop` aus
- `FeaturesProcessed + FeaturesFailed + FeaturesFiltered` ergibt die
  Gesamtzahl aller bereits entschiedenen Feature-Ergebnisse, die der
  Streaming-Pfad verarbeitet hat
- im Batch-Pfad bleiben erfolgreich geparste und vom Filter akzeptierte, aber
  noch nicht geflushte Features weiter pending; sie zaehlen bis zum Flush oder
  bis zum vorzeitigen Abbruch in keinen der drei Result-Zaehler
- `Progress` meldet nach jedem Feature-Ergebnis (inkl. gefilterter Features)
  die kumulativen Zaehler
- im Batch-Pfad bleibt `Progress` trotzdem pro Feature-Parse-Ergebnis definiert;
  zwischen zwei Flushes duerfen die gemeldeten Zaehler bei pending Erfolgen
  unveraendert bleiben
- wirft der Filter selbst eine Exception, wird das Feature als fehlgeschlagen
  behandelt (nicht als gefiltert), und der konfigurierte Fehlerkanal
  (`OnError(...)` bzw. `onError`) wird aufgerufen
- wirft der Filter `OperationCanceledException`, wird diese sofort propagiert
  (nicht als Feature-Fehler gezaehlt)
- `OnEnd` erhaelt den `StreamingResult` mit allen drei Zaehlern
- wenn kein `Filter` gesetzt ist, aendert sich nichts am bisherigen Verhalten
- der Filter arbeitet immer auf dem rohen `GmlFeature`, nicht auf dem
  Builder-Output (`TFeature`); bei `StreamingGml.ParseAsync<builder>` wird
  der Filter *vor* dem Builder ausgewertet

## Auswertungsreihenfolge

```text
GmlFeatureStreamParser.ParseItemsAsync
  |
  v
FeatureStreamItem
  |
  +-- IsSuccess == false --> failed++, OnError / onError
  |
  +-- IsSuccess == true
        |
        +-- Filter == null oder Filter(feature) == true
        |     |
        |     v
        |   OnFeature / onBatch / WriteFeatureAsync
        |     |
        |     +-- Erfolg --> processed++
        |     +-- Exception --> failed++, OnError / onError
        |
        +-- Filter(feature) == false --> filtered++
        |     |
        |     +-- item.Issues.Count > 0 --> zusaetzlich nicht-fatale
        |           Diagnostik ueber OnError / onError
        |
        +-- Filter(feature) wirft OperationCanceledException --> propagiert sofort
        |
        +-- Filter(feature) wirft andere Exception --> failed++, OnError / onError
```

## Im Batch-Pfad

- der Filter wird pro Feature *vor* dem Hinzufuegen zum Batch ausgewertet
- gefilterte Features landen nicht im Batch-Buffer
- der Batch-Zaehler (`batchSize`) zaehlt nur ungefilterte Features
- ungefilterte Features zaehlen erst dann als `FeaturesProcessed`, wenn der
  Batch erfolgreich geflusht wurde
- bis dahin bleiben sie pending; `Progress` darf deshalb zwischen zwei Flushes
  denselben Zaehlerstand mehrfach melden
- ein Batch mit 100 ungefilterten Features wird geflusht, auch wenn insgesamt
  500 Features geparst und 400 davon gefiltert wurden
- parsernahe `Issues` eines spaeter herausgefilterten Features werden trotzdem
  ueber den konfigurierten Fehlerkanal weitergereicht und nicht still verworfen

## Nutzung

### Property-Filter

```csharp
var result = await StreamingGml.ParseAsync(
    stream,
    new GeoJsonBuilder(),
    feature => ProcessAsync(feature),
    options: new StreamingParserOptions
    {
        Filter = f => f.Properties["status"] is GmlStringProperty { Value: "active" }
    });

// result.FeaturesProcessed -- nur aktive Features
// result.FeaturesFiltered  -- uebersprungene Features
```

### BBox-Clipping

```csharp
var (minX, minY, maxX, maxY) = (11.0, 47.0, 12.0, 48.0);

var result = await StreamingGml.ParseAsync(
    stream,
    new GeoJsonBuilder(),
    feature => ProcessAsync(feature),
    options: new StreamingParserOptions
    {
        Filter = f => HasPointInBBox(f, minX, minY, maxX, maxY)
    });

static bool HasPointInBBox(GmlFeature feature,
    double minX, double minY, double maxX, double maxY)
{
    foreach (var entry in feature.Properties)
    {
        if (entry.Value is GmlGeometryProperty gp
            && gp.Geometry is GmlPoint pt
            && pt.Coordinate.X >= minX
            && pt.Coordinate.X <= maxX
            && pt.Coordinate.Y >= minY
            && pt.Coordinate.Y <= maxY)
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
        Filter = f => f.Properties.Any(e => e.Value is GmlGeometryProperty)
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
- gefiltertes Feature mit parsernahen `Issues` → Diagnostik wird ueber
  `OnError(...)` bzw. `onError` weitergereicht, aber das Feature als
  `FeaturesFiltered` gezaehlt
- Filter + `ErrorBehavior.Stop` → Stop nach erstem Fehler, gefilterte Zaehler korrekt
- Filter + `ParseBatchesAsync` → Batch enthaelt nur ungefilterte Features
- Filter + `ParseBatchesAsync` → `batchSize` zaehlt ungefilterte Features
- Filter + `ParseBatchesAsync` → `Progress` bleibt pro Feature definiert und
  darf zwischen zwei Flushes denselben Zaehlerstand mehrfach melden
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

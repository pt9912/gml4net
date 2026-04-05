# Feature-Filter fuer den Streaming-Pfad

Status: Implementiert.

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

- `Filter` vom Typ `Func<GmlFeature, bool>?`
- entscheidet nach erfolgreichem Parsen, ob ein Feature an Callback, Batch oder
  Sink weitergereicht wird
- bei `false` wird das Feature als gefiltert gezaehlt
- vorhandene nicht-fatale Parser-Diagnostik bleibt dabei ueber den
  Fehlerkanal sichtbar

### StreamingResult

Siehe `src/Gml4Net/Parser/Streaming/StreamingResult.cs`.

Neues Property:

- `FeaturesFiltered`
- zaehlt erfolgreich geparste Features, die durch den konfigurierten Filter
  ausgeschlossen wurden

### StreamingProgress

Siehe `src/Gml4Net/Parser/Streaming/StreamingResult.cs`.

Neuer Parameter:

- `StreamingProgress` enthaelt zusaetzlich `FeaturesFiltered`
- Progress-Snapshots bestehen damit aus `FeaturesProcessed`,
  `FeaturesFailed` und `FeaturesFiltered`

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

Beispiel: Ein Property-Filter kann ueber `TryGetValue("status", out var v)`
auf der Property-Bag arbeiten und nur Features mit einem
`GmlStringProperty`-Wert `"active"` durchlassen. Im Result zaehlt
`FeaturesProcessed` dann nur aktive Features, waehrend `FeaturesFiltered` die
uebersprungenen Features enthaelt.

### BBox-Clipping

Beispiel: Fuer einfaches BBox-Clipping kann der Filter die
`GmlGeometryProperty`-Eintraege eines Features durchsuchen und ein Feature nur
dann akzeptieren, wenn mindestens ein enthaltenes `GmlPoint` innerhalb der
gewuenschten Bounding Box liegt.

### Kombiniert mit Fehlertoleranz

Beispiel: Der direkte `StreamingGmlParser` kann mit
`ErrorBehavior = Continue` und einem ID-basierten Filter kombiniert werden,
etwa um nur Features mit einem Praefix wie `building.` weiterzuverarbeiten.
Dabei koennen `OnFeature(...)` und `OnError(...)` parallel genutzt werden; im
Result spiegeln `FeaturesProcessed`, `FeaturesFailed` und `FeaturesFiltered`
anschliessend die Aufteilung wider.

### Batch mit Filter

Beispiel: Im Batch-Pfad kann ein Filter vor dem Batch-Buffer nur Features mit
Geometrie durchlassen. Die resultierenden Batches enthalten dann ausschliesslich
ungefilterte Features, und `batchSize` bezieht sich nur auf diese Teilmenge.

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

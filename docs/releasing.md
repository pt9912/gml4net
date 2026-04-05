# Releasing

## Pakete

| Paket | Beschreibung | Workflow |
|---|---|---|
| `Gml4Net` | Core-Bibliothek: Parser, Modell, Interop, OWS, WCS | `publish-gml4net.yml` |
| `Gml4Net.IO` | Optionales I/O-Paket: File, HTTP, Streaming | `publish-gml4net-io.yml` |

Beide Pakete werden auf [nuget.org](https://www.nuget.org/) veroeffentlicht.

## Voraussetzungen

- `NUGET_API_KEY` muss als Repository-Secret in den GitHub-Settings hinterlegt sein
  (Settings → Secrets and variables → Actions → Repository secrets)
- Das Secret muss in der GitHub Environment `nuget` verfuegbar sein
  (Settings → Environments → nuget)
- Der Key muss Berechtigungen fuer `Gml4Net` und `Gml4Net.IO` auf nuget.org haben

## Release per Git-Tag

Die Publish-Workflows werden automatisch ausgeloest wenn ein Tag mit dem
passenden Praefix gepusht wird.

### Beide Pakete mit derselben Version releasen

```bash
# Sicherstellen dass main aktuell ist
git checkout main
git pull

# Tags erstellen
git tag Gml4Net-v0.1.0
git tag Gml4Net.IO-v0.1.0

# Tags pushen (loest beide Workflows aus)
git push origin Gml4Net-v0.1.0 Gml4Net.IO-v0.1.0
```

### Einzelnes Paket releasen

```bash
# Nur Core
git tag Gml4Net-v0.2.0
git push origin Gml4Net-v0.2.0

# Nur I/O
git tag Gml4Net.IO-v0.2.0
git push origin Gml4Net.IO-v0.2.0
```

### Tag-Format

| Paket | Tag-Muster | Beispiele |
|---|---|---|
| `Gml4Net` | `Gml4Net-v<semver>` | `Gml4Net-v0.1.0`, `Gml4Net-v1.0.0`, `Gml4Net-v2.0.0-rc.1` |
| `Gml4Net.IO` | `Gml4Net.IO-v<semver>` | `Gml4Net.IO-v0.1.0`, `Gml4Net.IO-v1.0.0` |

Die Version muss gueltigem SemVer entsprechen (`major.minor.patch` mit
optionalem Pre-Release-Suffix).

## Release per workflow_dispatch

Alternativ kann ein Release manuell ueber die GitHub Actions UI ausgeloest
werden, ohne einen Tag zu erstellen:

1. GitHub → Actions → "Publish Gml4Net" (oder "Publish Gml4Net.IO")
2. "Run workflow" klicken
3. Version eingeben (z.B. `0.1.0`)
4. "Run workflow" bestaetigen

Dies ist nuetzlich fuer Testveroeffentlichungen oder wenn kein Tag
erwuenscht ist.

## Was der Workflow macht

Jeder Publish-Workflow fuehrt folgende Schritte aus:

1. **Checkout** -- Repository auschecken
2. **Version ermitteln** -- aus dem Tag-Namen oder dem `workflow_dispatch`-Input
3. **Test** -- `docker buildx build --target test` (vollstaendiger Testlauf mit Coverage-Gate)
4. **Pack** -- `docker buildx build --target artifacts` mit `PACK_TARGET` und `PACKAGE_VERSION`
5. **Upload** -- `.nupkg`-Datei als GitHub Actions Artifact sichern
6. **Push** -- `docker buildx build --target push` mit `NUGET_API_KEY` als BuildKit-Secret

Der Push verwendet `--skip-duplicate`, sodass ein erneutes Ausloesen mit
derselben Version keinen Fehler erzeugt.

## Versionierung

Das Projekt folgt [Semantic Versioning](https://semver.org/):

- **0.x.y** -- Initiale Entwicklung, API kann sich aendern
- **1.0.0** -- Erste stabile API
- **Major** -- Breaking Changes
- **Minor** -- Neue Features, abwaertskompatibel
- **Patch** -- Bugfixes, abwaertskompatibel

`Gml4Net` und `Gml4Net.IO` koennen unterschiedliche Versionen haben.
`Gml4Net.IO` referenziert `Gml4Net` als Projektabhaengigkeit -- beim
Release wird die zur Buildzeit aktuelle Version des Core-Pakets verwendet.

## Lokales Testen des Pack-Schritts

**Wichtig:** Immer `--target artifacts` verwenden (nicht `--target pack`).
Der `artifacts`-Stage basiert auf `FROM scratch` und enthaelt nur die
`.nupkg`-Dateien. Der `pack`-Stage wuerde das gesamte SDK-Dateisystem
(~1 GB) exportieren.

```bash
# Beide Pakete bauen
docker buildx build --target artifacts \
  --build-arg PACKAGE_VERSION=0.1.0 \
  -o type=local,dest=./artifacts .

# Nur Core-Paket bauen
docker buildx build --target artifacts \
  --build-arg PACK_TARGET=src/Gml4Net/Gml4Net.csproj \
  --build-arg PACKAGE_VERSION=0.1.0 \
  -o type=local,dest=./artifacts .

# Nur I/O-Paket bauen
docker buildx build --target artifacts \
  --build-arg PACK_TARGET=src/Gml4Net.IO/Gml4Net.IO.csproj \
  --build-arg PACKAGE_VERSION=0.1.0 \
  -o type=local,dest=./artifacts .
```

Die `.nupkg`-Dateien landen im `./artifacts/`-Verzeichnis (wenige KB).

## Checkliste vor einem Release

- [ ] Alle Tests gruen (`docker buildx build --target test .`)
- [ ] CHANGELOG.md aktualisiert (Unreleased → Versionsnummer + Datum)
- [ ] README.md Statusabschnitt aktuell
- [ ] Keine uncommitteten Aenderungen (`git status`)
- [ ] Main-Branch ist aktuell (`git pull`)
- [ ] Version noch nicht auf nuget.org vorhanden

## Wichtig: CHANGELOG vor dem Tag aktualisieren

Der Publish-Workflow baut das Paket aus dem Commit auf den der Tag zeigt.
Die CHANGELOG.md muss daher **vor** dem Erstellen des Tags committet und
gepusht werden, damit sie im Paket enthalten ist.

Reihenfolge:

1. CHANGELOG.md mit neuem Versionsabschnitt committen
2. `git push origin main`
3. Tags erstellen und pushen

Wenn die CHANGELOG erst nach dem Tag aktualisiert wird, enthaelt das
veroeffentlichte Paket die alte CHANGELOG.

## Bisherige Releases

| Version | Datum | Pakete | Anmerkung |
|---|---|---|---|
| 0.1.0 | 2026-04-04 | Gml4Net, Gml4Net.IO | Initiales Release, fehlende README in .nupkg |
| 0.1.1 | 2026-04-04 | Gml4Net, Gml4Net.IO | README in NuGet-Paketen eingebettet |
| 0.1.2 | 2026-04-04 | Gml4Net, Gml4Net.IO | Vollstaendige NuGet-Metadaten (Authors, URLs, Tags) |
| 0.1.3 | 2026-04-05 | Gml4Net, Gml4Net.IO | Streaming Public API, generischer Parser, IBuilder Rename |
| 0.1.4 | 2026-04-05 | Gml4Net, Gml4Net.IO | Fix Related Projects Links in README |
| 0.2.0 | 2026-04-05 | Gml4Net, Gml4Net.IO | Feature-Filter fuer Streaming-Pfad, Breaking: StreamingProgress 3 Parameter |

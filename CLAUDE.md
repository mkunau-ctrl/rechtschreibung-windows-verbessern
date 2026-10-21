# Rechtschreib-Trainer (Windows) — Einstieg

## Zweck

Tray-Programm für Windows, das beim Tippen wiederkehrende Vertipper und
Groß-/Kleinschreibfehler **live und offline** korrigiert (kein Netz, keine
KI). Daneben gibt es weiter einen Opt-in-Mitschreib-Modus, der getippten
Text protokolliert, um daraus neue Korrekturen abzuleiten.

## Wo weiterlesen

**Den Code musst du nicht lesen, um hier weiterzuarbeiten.** Diese zwei
Dateien reichen als Einstieg:

1. **`docs/DATEIEN.md`** — wie das Programm funktioniert (Ablauf beim Tippen,
   Schritt für Schritt), was jede Datei tut, wo welche Daten liegen, und die
   bekannten Fallstricke. **Zuerst hier lesen.**
2. **`docs/PROJEKT-LOG.md`** — was wann warum gemacht wurde, aktueller Stand,
   offene Punkte.

Bei Bedarf tiefer:

- `docs/superpowers/plans/2026-10-21-qualitaet-runde-3-plan.md` — die letzte große Runde: Analyse der echten Tippdaten (alle Zahlen) und was daraus gebaut wurde.
- `docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md` — der erste Plan zur Qualitätssteigerung (Phasen, Kennzahlen, Entscheidungen).
- `docs/RECHERCHE-KORREKTURSYSTEME.md` — wie Hunspell, Aspell, SymSpell, LanguageTool & Co. das lösen, mit Quellen und Lizenzen.
- `docs/superpowers/specs/2026-09-03-live-korrektur-offline-design.md` — das ursprüngliche Design der Live-Korrektur.
- `data/HERKUNFT.md` — Herkunft, Umfang und Lizenz aller Wortlisten.
- `README.md` — Nutzersicht: Funktionsweise, Sicherheitshinweise, Build-Befehle.

## Aufbau

- `src/RechtschreibTrainer.Core/` — reine Logik, unit-getestet, keine Windows-APIs:
  `OfflineCorrector`, `CorrectionDictionary`, `CorrectionRules`, `SpellCorrector`,
  `WordList`, `WordWatcher`, `LiveCorrectionController`, `LearnStore`, `HotkeySpec`,
  seit Runde 3 auch `KeyInput`, `CompoundSplitter`, `FunctionWords`,
  `Abbreviations`, `SecretTriggers`.
- `src/RechtschreibTrainer/` — WinExe-Tray-App: `TrayApp` (Zustand/Verdrahtung),
  `KeyboardHook`/`MouseHook` (Win32-Hooks), `Replacer` (SendInput),
  `DictionaryLoader`, `HotkeySettings`, `NeverCorrectStore`, `AppPaths` (alle
  Dateiorte), `IconFactory` (Tray-Icons je Zustand).
- `tests/RechtschreibTrainer.Core.Tests/` — xUnit, reale Ein-/Ausgaben, keine Mocks.
  **Hauptmesslatte: `AlltagBenchmarkTests` + `alltag-faelle.tsv`** (Fälle mit
  Satzkontext). Neue Fälle dort ergänzen — nur neutrale Sätze, nie private Texte.
- `tests/RechtschreibTrainer.Tests/` — Tests für Windows-nahe Teile (Icon, Replacer).
- `scripts/install.ps1` — Autostart-/Installationsskript.
- `scripts/fetch-fehlerlisten.ps1` — erzeugt `src/RechtschreibTrainer/haeufige-fehler.txt`
  neu (streng gefilterte Wikipedia-Tippfehlerliste + LibreOffice-Autokorrektur,
  ~1.000 Paare). Nur zur Entwicklungszeit; das Programm geht nie ins Netz.
- `scripts/erzeuge-wortdaten.py` — erzeugt `data/woerter.txt`, `data/schreibung.txt`
  (Groß/klein-Korpus), `data/klein-gueltig.txt` (Python 3, nur Entwicklungszeit).
  Danach `fetch-fehlerlisten.ps1` neu laufen lassen. Siehe `data/HERKUNFT.md`.
- Datendateien neben der .exe: `haeufige-fehler.txt` (automatisch),
  `standard-vertipper.txt`, `klassische-fehler.txt`,
  `data/woerter.txt`, `data/haeufigkeit.txt`, `data/substantive.txt`,
  `data/klein-schreiben.txt`, `data/namen.txt`, `data/schreibung.txt`,
  `data/klein-gueltig.txt`.
- Benutzerdaten liegen in `Dokumente\RechtschreibTrainer\` (Wörterbuch, Logs,
  Tastenbelegung, nie-korrigieren-Liste) — **nicht im Repo**, siehe `.gitignore`.

## Starten / Testen / Bauen

```powershell
cd src/RechtschreibTrainer
dotnet run
```

```powershell
dotnet test RechtschreibTrainer.sln
dotnet build RechtschreibTrainer.sln
```

Für eine eigenständige `.exe`:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

## Arbeitsweise

Dieses Projekt folgt dem Skill `projekt-workflow`: erst planen, dann bauen,
danach `docs/PROJEKT-LOG.md` und diese Datei aktualisieren. Antworten und
Doku auf Deutsch. Datenschutz beachten (siehe README-Abschnitt „Sicherheit").

## Konventionen / Fallstricke

- **Windows Defender blockiert gelegentlich `KeyboardHook.cs`** (Fehlalarm,
  weil ein systemweiter `WH_KEYBOARD_LL`-Hook technisch wie ein Keylogger
  aussieht). Wenn Build/Tests mit "enthält einen Virus" abbrechen oder
  git-Operationen an dieser Datei hängen bleiben: Windows-Sicherheit →
  Viren- & Bedrohungsschutz → Schutzverlauf → Erkennung zulassen/wiederherstellen,
  oder den Projektordner als Ausnahme eintragen. **Nicht** versuchen, den
  Code so umzuschreiben, dass er der Erkennung entgeht.
- **PC-Hub-Anbindung:** Das Programm schreibt `%APPDATA%\RechtschreibTrainer\status.json`
  (nur Zähler/Zustand, nie getippter Text; Pfad per Umgebungsvariable
  `RECHTSCHREIBTRAINER_STATUS` überschreibbar). Logik in `StatusTracker`/`StatusFileWriter` (Core).
- Hotkeys sind über `Dokumente\RechtschreibTrainer\tasten.txt` vom Nutzer
  änderbar (Standard: Strg+Alt+R Mitschreiben, Strg+Alt+P Pause, Strg+Alt+Z
  Undo). Schlägt eine Registrierung fehl, meldet die App das per Tray-Balloon.
- Push auf GitHub lief bisher direkt durch (kein Auto-Classifier-Block wie
  bei anderen Projekten des Nutzers) — trotzdem vor destruktiven Git-Aktionen
  immer erst Status prüfen.
- Aktueller Branch-Stand: `main` ist führend; `feat/haeufige-fehler-liste`,
  `feat/live-korrektur-offline`, `hub-anbindung` und `feat/qualitaet-runde-3`
  (2026-10-22) sind hineingemergt. Für neue Features neuen Feature-Branch von
  `main` abzweigen.
- **Datenschutz:** Der Hook protokolliert keine Einzelanschläge; Debug-Log ist
  standardmäßig aus (`debug.on` nicht anlegen, außer der Nutzer will es).
  Nach Passwort-Auslösewörtern werden 3 Tokens nicht angesehen. Auswertungen
  echter Tippdaten nur lokal (Scratchpad), nie Texte ins öffentliche Repo.
- **Dateien nicht mit PowerShell `Get-Content`/`Set-Content` umschreiben** —
  zerstört UTF-8-Umlaute (PS 5.1). Edit-Werkzeug oder Python nehmen.
- Python liegt unter `%LOCALAPPDATA%\Programs\Python\Python312\python.exe`
  (nicht im PATH der Shell-Sitzungen; `-I` verwenden).
- `haeufige-fehler.txt` **nicht von Hand pflegen** — wird von
  `scripts/fetch-fehlerlisten.ps1` überschrieben. Eigene Einträge gehören in
  `klassische-fehler.txt` (handgeprüft) oder die persönliche `woerterbuch.txt`.

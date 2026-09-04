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

- `docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md` — der laufende Plan zur Qualitätssteigerung (Phasen, Kennzahlen, Entscheidungen).
- `docs/RECHERCHE-KORREKTURSYSTEME.md` — wie Hunspell, Aspell, SymSpell, LanguageTool & Co. das lösen, mit Quellen und Lizenzen.
- `docs/superpowers/specs/2026-09-03-live-korrektur-offline-design.md` — das ursprüngliche Design der Live-Korrektur.
- `data/HERKUNFT.md` — Herkunft, Umfang und Lizenz aller Wortlisten.
- `README.md` — Nutzersicht: Funktionsweise, Sicherheitshinweise, Build-Befehle.

## Aufbau

- `src/RechtschreibTrainer.Core/` — reine Logik, unit-getestet, keine Windows-APIs:
  `OfflineCorrector`, `CorrectionDictionary`, `CorrectionRules`, `SpellCorrector`,
  `WordList`, `WordWatcher`, `LiveCorrectionController`, `LearnStore`, `HotkeySpec`.
- `src/RechtschreibTrainer/` — WinExe-Tray-App: `TrayApp` (Zustand/Verdrahtung),
  `KeyboardHook`/`MouseHook` (Win32-Hooks), `Replacer` (SendInput),
  `DictionaryLoader`, `HotkeySettings`, `NeverCorrectStore`, `AppPaths` (alle
  Dateiorte), `IconFactory` (Tray-Icons je Zustand).
- `tests/RechtschreibTrainer.Core.Tests/` — xUnit, reale Ein-/Ausgaben, keine Mocks.
- `tests/RechtschreibTrainer.Tests/` — Tests für Windows-nahe Teile (Icon, Replacer).
- `scripts/install.ps1` — Autostart-/Installationsskript.
- Datendateien neben der .exe: `standard-vertipper.txt`, `klassische-fehler.txt`,
  `data/woerter.txt`, `data/haeufigkeit.txt`, `data/substantive.txt`,
  `data/klein-schreiben.txt`, `data/namen.txt`.
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
- Hotkeys sind über `Dokumente\RechtschreibTrainer\tasten.txt` vom Nutzer
  änderbar (Standard: Strg+Alt+R Mitschreiben, Strg+Alt+P Pause, Strg+Alt+Z
  Undo). Schlägt eine Registrierung fehl, meldet die App das per Tray-Balloon.
- Push auf GitHub lief bisher direkt durch (kein Auto-Classifier-Block wie
  bei anderen Projekten des Nutzers) — trotzdem vor destruktiven Git-Aktionen
  immer erst Status prüfen.
- Aktueller Branch-Stand: `main` und `feat/live-korrektur-offline` sind
  identisch (gemerged, 2026-09-05). Für neue Features neuen Feature-Branch
  von `main` abzweigen.

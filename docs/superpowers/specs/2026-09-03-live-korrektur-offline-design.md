# Live-Korrektur (offline) — Design

Datum: 2026-09-03
Status: in Umsetzung (v1)

## Ziel

Aus dem passiven Mitschreib-Tool wird ein Programm, das dauerhaft im
Hintergrund läuft und **während des Tippens** wiederkehrende Vertipper und
Satzanfang-Großschreibung sofort korrigiert — offline, ohne KI, ohne Netz.

Die KI-Ebene (ganze Sätze über `claude -p`, kontextabhängige
Groß-/Kleinschreibung, Umformulieren im Verkäufer-/Unternehmer-Stil) ist
**v2** und in diesem Design nur so weit berücksichtigt, dass sie später
sauber andocken kann.

## Nicht-Ziele (v1)

- Keine KI, kein Netzwerkzugriff.
- Keine Einstellungs-GUI (Konfiguration über Textdateien).
- Kein Autostart.
- Keine kontextabhängige Substantiv-Großschreibung.
- Keine system-weite Korrektur über andere Mechanismen als Hook + SendInput.

## Verhalten

Das Programm läuft als Tray-App mit **grünem** Icon (bereit). Ein
Low-Level-Keyboard-Hook ist aktiv, solange die Live-Korrektur nicht pausiert
ist.

Bei jedem Wortgrenzen-Zeichen (Leerzeichen, Enter, `. , ! ? ; :`) wird das
gerade fertige Wort geprüft:

1. `OfflineCorrector` bestimmt aus Wörterbuch + Regeln + Satzanfang-Kontext
   eine Korrektur oder `null`.
2. Nur bei einem **sicheren** Treffer (exakter Wörterbucheintrag oder
   eindeutige Regel) ersetzt der `Replacer` das Wort: N Rücktasten +
   korrigiertes Wort (das Grenzzeichen bleibt stehen).
3. Die Ersetzung wird für Undo gemerkt und als `vorher→nachher` an
   `Dokumente\RechtschreibTrainer\korrekturen.jsonl` angehängt.

Der Wort-Puffer liegt ausschließlich im Speicher, umfasst nur das aktuelle
Wort und wird bei Fokuswechsel, Mausklick, Pfeil-/Pos1-/Ende-Tasten und nach
längerer Pause verworfen. Bei verworfenem Puffer feuert keine Ersetzung.

## Hotkeys

| Taste | Funktion |
|---|---|
| `Strg+Alt+P` | Live-Korrektur an/aus (Icon grün ↔ grau) |
| `Strg+Alt+Z` | letzte Ersetzung rückgängig |
| `Strg+Alt+R` | Mitschreib-Modus (unverändert, opt-in, `keystrokes.log`) |

## Bausteine

### RechtschreibTrainer.Core (reine Logik, unit-getestet)

- **`CorrectionDictionary`** — lädt `woerterbuch.txt` (Benutzer, editierbar) und
  die mitgelieferte `standard-vertipper.txt`; Zeilenformat `falsch=richtig`;
  Kommentare mit `#`; späterer Eintrag gewinnt. Lookup case-sensitiv, mit
  Fallback auf kleingeschriebene Form unter Erhalt der Ursprungs-Groß-/Kleinschreibung.
- **`CorrectionRules`** — geordnete Liste reiner Regeln:
  - Wortanfang `cih` → `ich`
  - `scg` → `sch` (überall im Wort)
  - Wortende `cg` → `ch`
  - `ei`/`ie`-Standardfehler in „schreib"-Stamm-Wörtern (feste Wortliste)
- **`OfflineCorrector`** — `Correct(string wort, WordContext ctx) → CorrectionResult`.
  Reihenfolge: Wörterbuch → Regeln → Satzanfang-Großschreibung. `WordContext`
  trägt mindestens `IstSatzanfang`. `CorrectionResult` = `{ Original, Korrigiert,
  HatKorrektur, Quelle }`.
- **`LearnStore`** — hängt `CorrectionRecord` (Zeitstempel, vorher, nachher,
  Quelle) als eine JSON-Zeile an eine Datei an. Reiner Dateipfad als Parameter.
- **`DictionaryDistiller`** (kann v1.1 sein) — verdichtet `korrekturen.jsonl`
  später zu Wörterbuch-Kandidaten; nicht auf dem kritischen Pfad.

### RechtschreibTrainer (WinExe, manuell getestet)

- **`WordWatcher`** — hört am bestehenden `KeyboardHook`, pflegt den
  Wort-Puffer und den Satzanfang-Zustand, verwirft bei den oben genannten
  Ereignissen, meldet bei Wortgrenze `(wort, ctx)`.
- **`Replacer`** — `SendInput`: Rücktasten + Text; hält die letzte Ersetzung
  für Undo.
- **`TrayApp`** — grünes/graues/blaues Icon, Menü (Pause, Wörterbuch öffnen,
  Log-Ordner, Beenden), Benachrichtigung mit Anzahl Änderungen.
- **`KeyboardHook`** — bestehend; ggf. um Sondertasten-Events (Pfeile, Pos1/Ende)
  erweitert, damit `WordWatcher` den Puffer verwerfen kann.

## Fehlerverhalten

- Wörterbuchdatei fehlt/kaputt → nur Standardliste, Hinweis-Benachrichtigung.
- `Replacer` schlägt fehl / Feld reagiert nicht → nichts anfassen.
- Puffer ungültig → Hotkey-Ersetzung macht nichts (kein „blindes" Ersetzen).

## Sicherheit

- Kein Netz in v1 → kein Datenabfluss.
- Kein Roh-Keystroke-Log (nur `vorher→nachher` echter Treffer).
- `Strg+Alt+P` als schneller Schalter vor Passworteingaben.
- v2 (KI): dann Passwortfeld-Erkennung ergänzen, bevor Text das Gerät verlässt.

## Tests

- `OfflineCorrector`, `CorrectionDictionary`, `CorrectionRules`, `LearnStore`:
  xUnit, reale Ein-/Ausgaben, keine Mocks.
- `WordWatcher`, `Replacer`, `TrayApp`, Hook: manuelle Checkliste (echte
  Tastatureingaben + Fenster nötig).

## Seed-Wörterbuch (aus keystrokes.log, 2026-09-03)

```
cih=ich
nocg=noch
scgauen=schauen
aknn=kann
richrig=richtig
auc=auch
feher=fehler
veressert=verbessert
benuzen=benutzen
eventeull=eventuell
überhauot=überhaupt
gepannt=gespannt
gepannnt=gespannt
korretur=korrektur
frae=frage
```

Bewusst ausgelassen (mehrdeutig / unsicher): `seht`→`sehr`, `micht`→`mich`,
`ststatur`→`tastatur`, `wjett`→`jetzt`.

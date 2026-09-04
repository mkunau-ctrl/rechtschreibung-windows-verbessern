# Projekt-Log — Rechtschreib-Trainer (Windows)

## 2026-09-05 – Phase 0 erledigt: die Messlatte steht, Ausgangszahlen gemessen

**Was:** Der Benchmark aus Plan-Phase 0 ist gebaut und liefert zum ersten Mal
harte Zahlen.

- `tests/RechtschreibTrainer.Core.Tests/benchmark-faelle.tsv` — **206
  beschriftete Fälle** aus `keystrokes.log` und `korrekturen.jsonl`, in vier
  Kategorien: `tippfehler` (muss korrigiert werden), `gross` (klein getipptes
  Substantiv/Eigenname), `korrekt` (darf nicht angefasst werden) und `kontext`
  (nur mit Satzzusammenhang entscheidbar, wird getrennt ausgewiesen).
- `BenchmarkTests.cs` — rechnet Präzision, Trefferquote und Fehlalarmrate aus
  und listet jeden Fehlgriff namentlich auf.
- `RepoFiles.cs` — lädt die echten Wortlisten und Vertipper-Dateien genau in
  der Reihenfolge, in der das laufende Programm sie lädt. Die persönliche
  `woerterbuch.txt` bleibt bewusst außen vor: gemessen wird der
  Auslieferungszustand, nicht ein einzelner PC.

**Die Ausgangsmessung (2026-09-05, vor jeder Verbesserung):**

| Kennzahl | Stand | Ziel |
|---|---|---|
| Präzision (von allem, was angefasst wird: wie viel war richtig) | **94,9 %** (94 von 99) | ≥ 98 % |
| Trefferquote (von allen nötigen Änderungen) | **82,5 %** (94 von 114) | ≥ 90 % |
| Fehlalarme (korrektes Wort angefasst) | **1,3 %** (1 von 78) | ≈ 0 % |
| Kontextabhängige Fälle fälschlich angefasst | 8 von 14 | Phase 4 |

**Der wichtigste Befund: Alle vier Falschersetzungen sind Umlaut-Fälle.**

```
moechte  -> mochte    (erwartet: möchte)
waehrend -> wahrend   (erwartet: während)
gruen    -> grauen    (erwartet: grün)
schoen   -> Schonen   (erwartet: schön)
```

Dazu kommen sechs weitere übersehene Wörter aus demselben Grund (`fuer`,
`ueberhauot`, `naemlih`, `zustaedig`, `ausfuerhren`, `auffaekt`). Die
Ersatzschreibung `ue/oe/ae` ist damit **die mit Abstand größte einzelne
Fehlerquelle** — und genau das, was die Hunspell-`REP`-Tabelle aus Phase 2
ohne jedes Raten löst. `gruen → grauen` zeigt auch, warum: Weil `ue → ü`
unbekannt ist, rät der Korrektor stattdessen wild in der Wortliste herum.

**Weitere Erkenntnis:** Ein Teil der im Log protokollierten Fehlgriffe ist
durch die Fixes vom 2026-09-04 **bereits behoben**. `skill→kill`,
`fuer→Feuer`, `datri→dari`, `ganzen→Ganzen`, `icht→nicht`, `kkann→kann`,
`ernn→renn` und `obne→oben` passieren heute nicht mehr — das Programm lässt
diese Wörter jetzt in Ruhe. Die Ausgangslage ist also besser als der rohe Log
vermuten ließ.

**Verbleibender Fehlalarm:** `codest → Codes` — hier zerstört der Rateschritt
ein korrektes Wort.

**Entscheidungen:**
- Der Benchmark-Test arbeitet als **Ratsche**: Geprüft wird gegen den zuletzt
  erreichten Stand, nicht gegen das Endziel. Damit ist die Testsuite grün,
  schlägt aber sofort an, wenn eine Änderung die Qualität verschlechtert.
  Wird etwas besser, werden die Konstanten `StandPraezision` /
  `StandTrefferquote` / `StandFehlalarme` in `BenchmarkTests.cs` hochgesetzt.
  Ein dauerhaft roter Test wäre wertlos — den schaut nach zwei Wochen niemand
  mehr an.
- Kontextabhängige Fälle werden **getrennt** ausgewiesen und nicht in die
  Hauptkennzahlen eingerechnet. Sie sind vor Phase 4 prinzipiell nicht
  lösbar; sie in die Zahlen zu mischen, würde den Fortschritt verschleiern.

**Stand danach:** 115 Tests grün (vorher 113). Kein Produktivcode geändert —
nur Messung.

**Offene Punkte / Nächste Schritte:**
- **Phase 1:** Ersetz-Verzögerung (`SettleTime` 130 ms bei 188 ms echtem
  Tastenabstand) und automatische Passwortfeld-Erkennung. Achtung: Dieser
  Fehler ist im Benchmark **nicht sichtbar**, weil er beim Ersetzen im echten
  Textfeld entsteht, nicht in der Logik. Er braucht einen eigenen Test bzw.
  eine manuelle Prüfung.
- **Phase 2:** Umlaut-Ersetzungstabelle nach Hunspell-Vorbild — laut Messung
  der größte Hebel: behebt 4 von 5 Falschersetzungen und 6 von 16 übersehenen
  Fällen.

## 2026-09-05 – Qualitätsplan, Recherche und vollständige Systemdokumentation

**Was:** Kein Code geändert — drei neue Dokumente und eine überarbeitete
`CLAUDE.md`:

- **`docs/DATEIEN.md`** (neu) — erklärt den kompletten Ablauf beim Tippen
  Schritt für Schritt, was jede einzelne Datei tut, wo welche Daten liegen und
  welche Fallstricke bekannt sind. Ziel: Eine neue Session (Mensch oder KI)
  versteht das System, **ohne den Quellcode lesen zu müssen**.
- **`docs/superpowers/plans/2026-09-05-korrektur-qualitaet-plan.md`** (neu) —
  Plan, um die Korrekturqualität messbar auf ≥ 98 % Präzision und ≥ 90 %
  Trefferquote zu bringen. Sechs Phasen, jede mit einer Messgröße.
- **`docs/RECHERCHE-KORREKTURSYSTEME.md`** (neu) — was Hunspell, Aspell,
  SymSpell, LanguageTool, Kölner Phonetik und die Fachliteratur zum Thema
  beitragen, mit Quellen, Lizenzen und der Bewertung, was wir übernehmen.

**Warum:** Der Nutzer will eine Korrektur, die „zu 90 % perfekt" läuft und
sich weiter verbessert — und er will nicht, dass jede neue Session erst den
ganzen Code lesen muss, um zu verstehen, was das Programm ist.

**Die wichtigsten Befunde:**

1. **Ein Timing-Fehler erzeugt vermutlich die Hälfte aller Fehlgriffe.** Aus
   `debug.log` gemessen: Tastenabstand im Median **188 ms** (p10 = 172 ms).
   `TrayApp.SettleTime` wartet aber nur **130 ms** — die Ersetzung feuert also
   mitten im Weitertippen, die simulierten Rücktasten treffen das nächste
   Wort. Im Korrektur-Log stehen entsprechend viele Bruchstücke (`ernn`,
   `itoniert`, `nstellen`, `ondern`, `icht`, `hochgel`), die dann auch noch
   „korrigiert" wurden.
2. **Umlaut-Ersatzschreibung fehlt**: `fuer→Feuer` statt `für`,
   `moechte→mochte`. Hunspell löst das mit einer festen Ersetzungstabelle
   (28 deutsche Einträge), die **vor** jedem Raten greift.
3. **Groß-/Kleinschreibung ohne Satzkontext** ist prinzipiell nicht sicher:
   `ganzen→Ganzen` war belegt falsch.
4. **Deutsche Komposita sind ein unterschätztes Risiko** (`Kältekreislauf`,
   `Nutzungslimit`): Sie stehen in keiner Wortliste und sehen wie Tippfehler
   aus. Bei Abstand 1 harmlos, bei Abstand 2 gefährlich → Zerlegungsprüfung
   wurde als neue Phase 2b in den Plan aufgenommen.
5. **Unsere Wortliste ist unvollständig**: Vom LibreOffice-Wörterbuch wurden
   nur die Stämme übernommen, die Affix-Regeln nie ausgewertet.

**Entscheidungen:**
- Zielwerte getrennt nach Präzision (≥ 98 %), Trefferquote (≥ 90 %) und
  Fehlalarmen (≈ 0 %). Präzision zählt mehr: Eine falsche Ersetzung wiegt
  schwerer als ein übersehener Tippfehler.
- Reihenfolge **0 → 1 → 2** (erst messen, dann Timing, dann sichere
  Ersetzungen), abgestimmt mit dem Nutzer.
- Speicherbudget bis ~500 MB, Start darf 2–3 Sekunden dauern.
- Phase 4 (Kontext/Bigramme) ist gesetzt, mit der Auflage, dass das
  Wortfenster nur im Arbeitsspeicher lebt und nie in eine Datei gelangt.
- **Bei `SendInput` bleiben**, Timing reparieren. Das Text Services Framework
  wäre technisch sauberer (kein Wettlauf bauartbedingt), ist aber als
  COM-Textdienst im Prozess jeder Fremdanwendung zu aufwendig und für
  Virenscanner noch verdächtiger. Als Option dokumentiert.
- Neu aufgenommen: automatische Passwortfeld-Erkennung (`ES_PASSWORD`) als
  **zusätzliche** Absicherung — sie greift nicht in Browsern und modernen
  Oberflächen und ersetzt den Pause-Hotkey deshalb nicht.

**Stand danach:** Plan freigegeben, noch keine Zeile Code geändert. Build und
Tests unverändert grün (113 Tests).

**Offene Punkte / Nächste Schritte:**
- **Phase 0, erste Aufgabe:** Alle 82 Korrekturen aus `korrekturen.jsonl`
  gegen den heutigen Stand nachrechnen — ein Teil der protokollierten
  Fehlgriffe stammt aus der Zeit **vor** den Fixes vom 2026-09-04 (11:58 und
  12:17) und ist womöglich schon erledigt. Ergebnis ist die ehrliche
  Ausgangszahl.
- Danach Phase 1 (Timing + Passwortfelder), dann Phase 2 (sichere
  Ersetzungen: Hunspell-REP-Tabelle + gefilterte Wikipedia-Tippfehlerliste).

## 2026-09-05 – Feature-Branch nach main gemergt, Projekt-Doku nachgezogen

**Was:** Der Branch `feat/live-korrektur-offline` (9 Commits, seit
2026-09-03 auf GitHub gepusht, aber noch nicht in `main`) wurde per
`git merge --no-ff` nach `main` gemergt und gepusht (Merge-Commit
`ccde2ce`). Danach `CLAUDE.md` und dieses Log neu angelegt, da die
bisherigen Sessions das noch nicht getan hatten.

**Warum:** Sitzung wurde mit "weiter machen am GitHub-Projekt Autokorrektur"
begonnen. Vor dem Weiterbauen sollte der Branch-Stand konsolidiert und die
Doku auf den aktuellen Stand gebracht werden (Skill `projekt-workflow`).

**Stolperstein unterwegs:** Beim Merge blockierte Windows Defender aktiv den
Lese-/Schreibzugriff auf `src/RechtschreibTrainer/KeyboardHook.cs`
("enthält einen Virus oder möglicherweise unerwünschte Software") —
vermutlich ein Fehlalarm, da ein `WH_KEYBOARD_LL`-Hook technisch wie ein
Keylogger aussieht. Das brachte `git merge`/`git stash` kurz in einen
unfertigen Zustand (nichts verloren, beide Branches lagen vollständig auf
GitHub). Nutzer hat die Erkennung in Windows-Sicherheit zugelassen, danach
lief Merge, Build und Test sauber durch. **Der Code wurde bewusst nicht
verändert, um die Erkennung zu umgehen** — nur die Windows-Einstellung war
nötig.

**Entscheidungen:**
- Merge statt Rebase, damit die Historie des Feature-Branches sichtbar bleibt.
- Keine inhaltlichen Code-Änderungen in diesem Schritt, nur Konsolidierung + Doku.

**Stand danach:**
- `main` und `feat/live-korrektur-offline` sind identisch, beide auf GitHub aktuell.
- `dotnet build RechtschreibTrainer.sln` und `dotnet test RechtschreibTrainer.sln`
  laufen fehlerfrei durch: 113 Tests grün (109 Core + 4 Tray-nah).
- Live-Korrektur v1 ist vollständig umgesetzt: Wörterbuch- und Regel-basierte
  Korrektur, Satzanfang- und Substantiv-Großschreibung, Namen-Ausnahmeliste
  (23k Namen, exakte Schreibweise), Undo-Hotkey, konfigurierbare Hotkeys,
  Typing-Debounce (~130 ms) gegen Race mit Weitertippen.
- `CLAUDE.md` und `docs/PROJEKT-LOG.md` existieren jetzt und sind aktuell.

**Offene Punkte / Nächste Schritte:**
- `DictionaryDistiller` (laut Spec als v1.1 vorgesehen): `korrekturen.jsonl`
  automatisch zu neuen Wörterbuch-Kandidaten verdichten — noch nicht gebaut.
- v2 laut Spec (bewusst nicht in v1): KI-Ebene über `claude -p` für ganze
  Sätze, kontextabhängige Substantiv-Großschreibung, Umformulieren im
  Verkäufer-/Unternehmer-Stil. Dafür vorher: Passwortfeld-Erkennung, bevor
  Text das Gerät verlassen könnte (siehe Spec, Abschnitt „Sicherheit").
- Manuelle Checkliste für `WordWatcher`/`Replacer`/`TrayApp`/Hook (laut Spec
  nur manuell testbar) ist noch nicht als Dokument festgehalten.

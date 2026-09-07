# Plan: Häufige-Fehler-Liste aus Internet-Trainingsdaten

Stand: 2026-09-07. Umgesetzt am selben Tag — siehe `docs/PROJEKT-LOG.md`.

## Wunsch des Nutzers

Trainingsdaten aus dem Internet sammeln (häufige deutsche Rechtschreib- und
Tippfehler) und ins Projekt einbauen, damit die Offline-Korrektur besser wird.
Ausdrücklich: **Präzision zuerst** — neue Daten nur, wenn die
Benchmark-Präzision nicht sinkt.

## Rahmen

- Datenquellen (vom Nutzer freigegeben): fertige Häufige-Fehler-Listen +
  frei lizenzierte Korpora. Alles nur als **kuratierte Offline-Listen** —
  das laufende Programm geht weiter nie ins Netz.
- **Nicht** dabei (YAGNI / Präzisionsrisiko): Laufzeit-n-Gramme,
  Confusion-Matrix aus dem GitHub Typo Corpus, Änderung der
  Groß-/Kleinschreib-Regel. Bleiben offene Punkte.

## Vorgehen

1. **`scripts/fetch-fehlerlisten.ps1`** — lädt die Wikipedia-„Liste von
   Tippfehlern" (`?action=raw`, `{{tippfehler|falsch|richtig}}`).
2. **Präzisions-Filter** (ein Paar kommt nur rein, wenn *alle* zutreffen):
   - `falsch` ist kein bekanntes Wort (woerter/substantive/namen),
   - `richtig` ist belegt (jedes Token bekannt **und** im Frequenzkorpus
     `haeufigkeit.txt` oder Substantivform — Korpus als Plausibilitätsprüfung),
   - kein reiner Groß-/Klein- oder ss/ß-Unterschied,
   - nicht von `ReplacementTable` oder den Handlisten abgedeckt,
   - nur Buchstaben/Bindestrich, ≥ 3 Zeichen, kein `*`/`+`,
   - „X oder Y"-Anmerkungen verworfen.
3. **`src/RechtschreibTrainer/haeufige-fehler.txt`** — die gefilterte Liste,
   getrennt von der handgemachten `klassische-fehler.txt`. Verdrahtet in
   `AppPaths`, `.csproj`, `DictionaryLoader` (zuerst geladen, Handlisten
   gewinnen bei Konflikt), `RepoFiles` (Benchmark misst die Liste mit).
4. **Tests:**
   - `HaeufigeFehlerListeTests` — Struktur/Sicherheit der Liste.
   - `BenchmarkTests` — Präzision darf nicht sinken (Ratsche bleibt 100 %).
   - `GaengigeTippfehlerBenchmarkTests` — 76 bewusst ausgesparte Tippfehler
     (`gaengige-tippfehler-holdout.tsv`): misst die Trefferquote der Kette auf
     *ungesehenen* Fehlern. Keine Ratsche, nur Analyse.
5. **Doku:** `HERKUNFT.md`, `DATEIEN.md`, `PROJEKT-LOG.md`, `CLAUDE.md`.

## Erwartung (ehrlich)

Eine exakte Vertipper-Liste ist von Natur aus präzise (exakter
Schlüssel-Lookup), solange der „falsch ist kein echtes Wort"-Filter hält —
dafür sind die Tests da. „Verstehen" ist damit nicht drin; das bräuchte
Sprachkontext oder KI und ist bewusst nicht Teil dieses Plans.

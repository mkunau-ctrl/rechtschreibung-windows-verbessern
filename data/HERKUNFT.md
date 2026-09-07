# Herkunft der Daten

## `woerter.txt` — 881.698 deutsche Wortformen

Zusammengeführt aus zwei Quellen, damit sowohl gebeugte Formen als auch
Eigennamen abgedeckt sind:

1. **[enz/german-wordlist](https://github.com/enz/german-wordlist)** — 685.828
   Vollformen inklusive Beugungen (`Häuser`, `gehst`, `personalisierter`).
   Lizenz: **CC0-1.0** (gemeinfrei).
   Nach Scrabble-Regeln zusammengestellt, enthält deshalb **keine Eigennamen**.

2. **Stämme aus [LibreOffice/dictionaries](https://github.com/LibreOffice/dictionaries)**
   `de/de_DE_frami.dic` (abgeleitet von igerman98) — 250.836 Einträge, mit
   Eigennamen wie `Claude`, `Berlin`, `Windows`.
   Lizenz: **GPL-2.0 / LGPL-2.1 / MPL-1.1** (Dreifachlizenz).
   Nur die Stämme übernommen; die Affix-Regeln der `.aff` sind nicht ausgewertet.

## `substantive.txt` — 258.182 großgeschriebene Substantivformen

Aus **[gambolputty/german-nouns](https://github.com/gambolputty/german-nouns)**
(aus dem deutschen Wiktionary abgeleitet). Lizenz: **CC-BY-SA-4.0**.
Alle Kasusformen der Lemmata; nominalisierte Infinitive („das Essen",
„das Gehen" — Neutrum, kein Plural, endet auf -en) sind herausgefiltert,
damit getippte Verben nicht großgeschrieben werden.

## `klein-schreiben.txt` — ~1.500 Wörter, die klein bleiben

Handverlesene geschlossene Wortklassen (Artikel, Pronomen, Konjunktionen,
Präpositionen, Hilfs-/Modalverben) + häufige Verben + die häufigsten
nicht-substantivischen Wörter aus dem Häufigkeitskorpus. Verhindert, dass
gleichlautende Substantive/Ortsnamen („Weil", „Essen") ein klein
geschriebenes Funktionswort großziehen.

## `haeufigkeit.txt` — 200.000 Wörter mit Häufigkeit

Aus **[hermitdave/FrequencyWords](https://github.com/hermitdave/FrequencyWords)**,
`content/2018/de/de_full.txt` (Untertitel-Korpus OpenSubtitles), auf die
häufigsten 200.000 gekürzt. Lizenz: **MIT**.

Dient nur dazu, gleich plausible Korrektur-Kandidaten zu sortieren.

## `src/RechtschreibTrainer/haeufige-fehler.txt` — ~840 häufige Tippfehler

Aus **[Wikipedia: Liste von Tippfehlern](https://de.wikipedia.org/wiki/Wikipedia:Liste_von_Tippfehlern)**
(Unterseiten A–Z, 0-9, PQ, XYZ, Sonderzeichen), maschinenlesbar über
`?action=raw` als `{{tippfehler|falsch|richtig}}`. Lizenz: **CC BY-SA 4.0**
(Wikipedia-Text); die abgeleitete Liste steht unter derselben Lizenz.

Erzeugt von `scripts/fetch-fehlerlisten.ps1`. Streng gefiltert, damit keine
Falschkorrektur entstehen kann:

- `falsch` ist **kein** bekanntes Wort (nicht in `woerter.txt` /
  `substantive.txt` / `namen.txt`),
- `richtig` ist belegt (jedes Token bekannt **und** im Frequenzkorpus
  `haeufigkeit.txt` oder als Substantivform),
- kein reiner Groß-/Klein- oder `ss`/`ß`-Unterschied (Satzkontext / Schweiz),
- nicht schon von `ReplacementTable` (`ue→ü` …) oder den Handlisten abgedeckt,
- „X oder Y"-Anmerkungen und Wortform-Platzhalter (`*`, `+`) verworfen.

Von den ~2.550 Rohvorlagen bleiben nach diesem Filter ~840 Paare übrig. 76
weitere, absichtlich ausgesparte Fälle liegen in
`tests/RechtschreibTrainer.Core.Tests/gaengige-tippfehler-holdout.tsv` und
dienen dem Trefferquoten-Test (`GaengigeTippfehlerBenchmarkTests`).

## Neu erzeugen

```bash
curl -sSLo scrabble.txt https://raw.githubusercontent.com/enz/german-wordlist/master/words
curl -sSLo de_frami.dic https://raw.githubusercontent.com/LibreOffice/dictionaries/master/de/de_DE_frami.dic
curl -sSLo freq.txt     https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/de/de_full.txt

cut -d'/' -f1 de_frami.dic | tail -n +2 | grep -v '^#' | grep -v '^$' > staemme.txt
cat scrabble.txt staemme.txt | sort -u > data/woerter.txt
head -200000 freq.txt > data/haeufigkeit.txt
```

Die Tippfehlerliste (`haeufige-fehler.txt`) braucht die obigen Wortlisten und
wird dann so erzeugt (PowerShell, Windows):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\fetch-fehlerlisten.ps1
```

# Herkunft der Daten

## `woerter.txt` — ~788.000 deutsche Wortformen

**Seit 2026-10-22 erzeugt von `python scripts/erzeuge-wortdaten.py`** (siehe
unten „Neu erzeugen"). Bis dahin war die Datei fehlerhaft: Die frami-Stämme
sind ISO-8859-1 kodiert und wurden ungewandelt angehängt (57.371 kaputte
Zeilen), und die Kennzeichen der `.dic` wurden ignoriert — damit galten 393
**verbotene Fehlschreibungen** (Kennzeichen `d`: `Wiederspruch`, `Rückgrad`,
`Arbeitsgeber`) als richtig, und 82.447 Nur-Kompositum-Stämme (`o`: klein
geschriebenes `auto`, `laptop`) sowie 5.749 Brauchtaffix-Stämme (`h`) als
eigene Wörter. Das Skript wandelt korrekt und filtert `d`, `o`, `h`; was enz
selbst kennt (`erstmal`, in frami verboten), bleibt.

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

## `schreibung.txt` — Groß/klein mitten im Satz (44.058 Wörter)

Aus den deutschen Sätzen von **[Tatoeba](https://tatoeba.org)**
(`downloads.tatoeba.org/exports/per_language/deu/deu_sentences.tsv.bz2`,
784.113 Sätze). Lizenz: **CC BY 2.0 FR** (Namensnennung: Tatoeba-Projekt und
seine Beitragenden). Format `wort gross klein`: wie oft das Wort **mitten im
Satz** (erstes Wort jedes Teilsatzes übersprungen) groß bzw. klein stand. Nur
Wörter, die es auch als Substantiv oder Name gibt. Gespeichert werden nur diese
Zählungen, keine Sätze. Grundlage der Großschreib-Regel (`WordList.IsCapitalisedOnly`).

## `klein-gueltig.txt` — 7.904 Wörter

Wörter, die in **enz/german-wordlist** (CC0) klein vorkommen **und** groß ein
Substantiv (`substantive.txt`) oder Name (`namen.txt`) sind: `testen`/`Testen`,
`leider`/`Leider`, `anders`/`Anders`. Ohne Korpusbeleg werden sie nur mit
Substantiv-Beleg großgeschrieben. Bewusst nur aus enz, weil frami auch klein
geschriebene Kompositum-Teile enthält.

## `src/RechtschreibTrainer/haeufige-fehler.txt` — ~1.000 häufige Tippfehler

**Zweite Quelle seit 2026-10-22:** LibreOffice-Autokorrektur Deutsch,
[`extras/source/autocorr/lang/de/DocumentList.xml`](https://github.com/LibreOffice/core/blob/master/extras/source/autocorr/lang/de/DocumentList.xml),
Lizenz **MPL-2.0**, derselbe Filter (161 zusätzliche Paare, 1.006 gesamt).

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
curl -sSLo freq.txt https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/de/de_full.txt
head -200000 freq.txt > data/haeufigkeit.txt
# substantive.txt, namen.txt, klein-schreiben.txt: siehe oben

# woerter.txt, klein-gueltig.txt, schreibung.txt (braucht substantive.txt + namen.txt):
python scripts/erzeuge-wortdaten.py
```

⚠️ **Nicht mehr** `cut -d'/' -f1 de_frami.dic | … > woerter.txt` benutzen —
das ignoriert Kodierung und Kennzeichen (siehe oben).

Die Tippfehlerliste (`haeufige-fehler.txt`) braucht die obigen Wortlisten und
wird dann so erzeugt (PowerShell, Windows):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\fetch-fehlerlisten.ps1
```

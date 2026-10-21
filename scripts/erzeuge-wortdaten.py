"""Erzeugt die Wortdaten in data/ neu (nur zur Entwicklungszeit, das Programm
selbst geht nie ins Netz).

    python scripts/erzeuge-wortdaten.py

Schreibt:
  data/woerter.txt        enz/german-wordlist (CC0) + Staemme aus LibreOffice
                          de_DE_frami.dic. Die .dic ist ISO-8859-1 kodiert
                          ("SET ISO8859-1" in der .aff) und wird hier korrekt
                          nach UTF-8 gewandelt - bis 2026-10-21 fehlte das, und
                          57.371 Zeilen waren kaputt (z. B. "?ppigkeit").
                          Verbotene Schreibweisen und Nur-Kompositum-Staemme
                          werden ausgefiltert (siehe unten).
  data/klein-gueltig.txt  Woerter, die klein ein gueltiges Wort sind UND gross
                          ein Substantiv oder Name ("testen"/"Testen",
                          "leider"/"Leider", "anders"/"Anders").
                          Quelle nur enz, weil die frami-Staemme auch klein
                          geschriebene Kompositum-Anfaenge enthalten ("auto").
  data/schreibung.txt     "wort gross klein": wie oft ein Wort im Tatoeba-
                          Korpus (deutsche Saetze, CC BY 2.0 FR) MITTEN im Satz
                          gross bzw. klein steht. Nur Woerter, die es auch als
                          Substantiv oder Name gibt. Grundlage der
                          Grossschreib-Regel.

Braucht data/substantive.txt und data/namen.txt (siehe data/HERKUNFT.md).
Laufzeit ~1 Minute.
"""
import bz2
import io
import os
import re
import sys
import urllib.request
from collections import Counter

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(REPO, "data")

ENZ = "https://raw.githubusercontent.com/enz/german-wordlist/master/words"
FRAMI = "https://raw.githubusercontent.com/LibreOffice/dictionaries/master/de/de_DE_frami.dic"
TATOEBA = "https://downloads.tatoeba.org/exports/per_language/deu/deu_sentences.tsv.bz2"


def hole(url: str) -> bytes:
    print("lade", url, file=sys.stderr)
    with urllib.request.urlopen(url, timeout=120) as r:
        return r.read()


def schreibe(name: str, zeilen) -> None:
    pfad = os.path.join(DATA, name)
    with open(pfad, "w", encoding="utf-8", newline="\n") as f:
        for z in zeilen:
            f.write(z + "\n")
    print("geschrieben", pfad, file=sys.stderr)


def main() -> None:
    enz = [w.strip() for w in hole(ENZ).decode("utf-8").splitlines() if w.strip()]

    # Kennzeichen aus de_DE_frami.aff (ein Zeichen je Kennzeichen):
    #   d = FORBIDDENWORD   ausdruecklich FALSCHE Schreibweise ("Wiederspruch",
    #                       "Rueckgrad", "Arbeitsgeber") - 395 Stueck
    #   o = ONLYINCOMPOUND  nur als Teil einer Zusammensetzung ("auto", "laptop")
    #   h = NEEDAFFIX       nur mit Endung gueltig
    # Bis 2026-10-21 wurden alle Staemme ungefiltert uebernommen - die
    # verbotenen Fehlschreibungen galten damit als richtig und wurden nie
    # korrigiert, und klein geschriebene Kompositum-Teile galten als Woerter.
    frami_roh = hole(FRAMI).decode("iso-8859-1").splitlines()
    staemme, verboten = [], set()
    for zeile in frami_roh[1:]:  # erste Zeile = Anzahl
        if not zeile.strip() or zeile.startswith("#"):
            continue
        eintrag = zeile.split()[0]
        stamm, _, kennzeichen = eintrag.partition("/")
        stamm = stamm.strip()
        if not stamm:
            continue
        if "d" in kennzeichen:
            verboten.add(stamm)
            continue
        if "o" in kennzeichen or "h" in kennzeichen:
            continue
        staemme.append(stamm)

    # Verbotenes gilt nur fuer frami selbst: Was enz kennt, bleibt ("erstmal"
    # ist in frami verboten - Duden will "erst mal" -, ist aber alltaeglich).
    woerter = sorted(set(enz) | set(staemme))
    print(f"frami: {len(staemme)} Staemme uebernommen, {len(verboten)} verbotene Schreibweisen ausgeschlossen",
          file=sys.stderr)
    schreibe("woerter.txt", woerter)

    substantive = set()
    with open(os.path.join(DATA, "substantive.txt"), encoding="utf-8") as f:
        for z in f:
            z = z.strip()
            if z:
                substantive.add(z)

    # Namen (Anders, Lasse, Will …) gehoeren dazu: klein sind das oft
    # gewoehnliche Woerter, und ob sie im Satz als Name gemeint sind, entscheidet
    # dann ebenfalls der Korpus.
    namen = set()
    with open(os.path.join(DATA, "namen.txt"), encoding="utf-8") as f:
        for z in f:
            z = z.strip()
            if z and not z.startswith("#"):
                namen.add(z)
    gross_moeglich = substantive | namen

    enz_set = set(enz)
    klein_gueltig = sorted(
        w for w in enz_set
        if w and w[0].islower() and (w[0].upper() + w[1:]) in gross_moeglich
    )
    schreibe("klein-gueltig.txt", klein_gueltig)

    # Tatoeba: jeden Satz an . ! ? : ; und Anfuehrungszeichen in Teilsaetze
    # zerlegen und das jeweils erste Wort ueberspringen - dort ist
    # Grossschreibung Pflicht und sagt nichts ueber die Wortart.
    token = re.compile(r"[A-Za-zÄÖÜäöüß]+(?:-[A-Za-zÄÖÜäöüß]+)*")
    trenner = re.compile(r"[.!?:;\"„“»«]")
    gross, klein = Counter(), Counter()
    roh = bz2.decompress(hole(TATOEBA)).decode("utf-8")
    for zeile in io.StringIO(roh):
        teile = zeile.rstrip("\n").split("\t")
        if len(teile) < 3:
            continue
        for abschnitt in trenner.split(teile[2]):
            for w in token.findall(abschnitt)[1:]:
                (gross if w[0].isupper() else klein)[w.lower()] += 1

    gross_moeglich_klein = {s.lower() for s in gross_moeglich}
    zeilen = [
        f"{w} {gross[w]} {klein[w]}"
        for w in sorted(set(gross) | set(klein))
        if w in gross_moeglich_klein
    ]
    schreibe("schreibung.txt", zeilen)


if __name__ == "__main__":
    main()

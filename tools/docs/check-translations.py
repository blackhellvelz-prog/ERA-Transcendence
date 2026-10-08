"""Checks that every English doc (X.md) and its Russian twin (X.ru.md) carry the same code spans and numbers.

Usage: python tools/docs/check-translations.py [--verbose]
Reports, for each pair, the technical inline code spans (`...`) and the numbers of two or more digits found in one
file but not the other. Ignored as legitimately translated: spans with Cyrillic or with an English phrase (two words
or more), placeholders (<...>), links to a twin (X.ru.md and X.md), dates, the language switch line. A number is
compared without thousands separators. Exit code 1 when a pair differs.
"""
import collections
import os
import re
import subprocess
import sys

CODE = re.compile(r"`([^`\n]+)`")
NUMBER = re.compile(r"(?<![\w.#/-])\d{1,3}(?:[ ,  ]\d{3})+(?![\w])|(?<![\w.#/-])\d+(?:\.\d+)?(?![\w])")
PHRASE = re.compile(r"[A-Za-z]{2,} [A-Za-z]{2,}")
CYRILLIC = re.compile(r"[Ѐ-ӿ]")
DATE = re.compile(r"\d{4}-\d{2}-\d{2}|\d{1,2}\.\d{1,2}\.\d{2,4}|\d{1,2}\.\d{2}(?=\D)")


def technical(span):
    return not CYRILLIC.search(span) and not PHRASE.search(span) and "<" not in span


def facts(text):
    lines = [l for l in text.replace("\r\n", "\n").split("\n") if not ("English" in l and "Русский" in l)]
    text = "\n".join(lines).replace(".ru.md", ".md")
    code = collections.Counter(c for c in CODE.findall(text) if technical(c))
    plain = CODE.sub(" ", text)
    plain = re.sub(r"\]\([^)]*\)", "]", plain)  # link targets
    plain = DATE.sub(" ", plain)
    numbers = (re.sub(r"[ ,  ]", "", n) for n in NUMBER.findall(plain))
    nums = collections.Counter(n for n in numbers if len(n.split(".")[0]) >= 2)
    return code, nums


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    verbose = "--verbose" in sys.argv
    files = subprocess.run(["git", "ls-files", "*.ru.md"], capture_output=True, text=True, encoding="utf-8").stdout.split("\n")
    bad = 0
    for ru in filter(None, files):
        en = ru[: -len(".ru.md")] + ".md"
        if not os.path.exists(en):
            continue
        ce, ne = facts(open(en, encoding="utf-8-sig").read())
        cr, nr = facts(open(ru, encoding="utf-8-sig").read())
        diffs = []
        for name, a, b in (("code", ce, cr), ("numbers", ne, nr)):
            only_en = sorted((a - b).elements())
            only_ru = sorted((b - a).elements())
            if only_en or only_ru:
                diffs.append(f"  {name}: only EN {only_en[:15]}{' …' if len(only_en) > 15 else ''}; "
                             f"only RU {only_ru[:15]}{' …' if len(only_ru) > 15 else ''}")
        if diffs:
            bad += 1
            print(en)
            if verbose:
                print("\n".join(diffs))
    print(f"{bad} pair(s) differ")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())

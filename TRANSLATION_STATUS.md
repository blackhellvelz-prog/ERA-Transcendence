# Documentation translation status / Статус перевода документации

**Goal (user request):** English is the primary documentation language (`X.md`); a synchronized Russian copy lives
next to each document (`X.ru.md`). / **Цель:** основная документация на английском (`X.md`), рядом русская копия
(`X.ru.md`).

Work state as of 2026-10-08 / Состояние на 08.10.2026:

| Step | State |
|------|-------|
| Russian twins `*.ru.md` for all 29 hand-written docs (language switch line, references to `.ru.md`) | done (commit "Add Russian twins…") |
| English translation of all 29 docs (`X.md`) | done |
| Independent adversarial verification of each translation | done for 23 docs; **not done for** `WoG_ReverseEngineering/03_ERM_Receivers.md`, `04_Commanders.md`, `05_Creature_Experience.md`, `06_WoG_Options.md`, `07_Battle_Map_Towns_Features.md`, `08_Save_Load.md` |
| Receiver notes in code → English, Russian dictionary for docs, `compat [--era] [--lang ru]` emits whole files, 4 generated tables regenerated (`Compatibility/ERM_Compatibility*.md` + `.ru.md`), new test `ReceiverRegistryTests` | done; build clean, 136 tests pass, ERA corpus passes |
| `Compatibility/options-defaults.json` (English names + `nameRu`), `id-maps/creature.json` notes | done |
| Scripts `tools/oe-recon/collect.ps1`, `tools/fetch-references/*` English-first (BOM+CRLF kept) | done |
| Language switch line at the top of each English hand-written doc: `**English** \| [Русский](X.ru.md)` | done (2026-10-08) |
| Cross-document consistency review (terminology, README/CLAUDE/HANDOFF rules, matrix columns) | README, CLAUDE, HANDOFF rewritten for the current state in both languages (2026-10-08); the rest **todo** |
| Automated check: identical code spans and numbers in `X.md` vs `X.ru.md` | done: `tools/docs/check-translations.py`; the remaining differences are translated placeholders (`$type` → `$тип`), number formats and old MODLOG entries |
| MODLOG entry about the bilingual change (EN in `MODLOG.md`, RU in `MODLOG.ru.md`) | done (2026-10-08 entry) |

## How to finish (for the local Claude Code) / Как закончить

1. Verify the 6 remaining translations against their `.ru.md` (sentence by sentence, tables, code spans, numbers);
   fix the English files only. The original prompts are in `tools/handoff/bilingual-docs-workflow.js`
   (`verifyPrompt`, `GLOSSARY`); it can be re-run as a workflow with `args.docs` = the 6 docs, or done by hand.
2. Run the final consistency review (prompt `critic` in the same file).
3. Add the English language switch line to every hand-written `X.md` that has an `X.ru.md` (generated tables
   already have it); do not add it to `CLAUDE.md`'s first heading position if it disturbs Claude Code — a first
   line is fine.
4. Check code spans/numbers: for each pair, the multiset of `` `…` `` spans and numbers should match (allow
   intended differences: `.md` vs `.ru.md` references, the documentation-language rule).
5. `dotnet build WoGOldenEra.sln`, `dotnet test tests/WoG.Tests` (+ ERA/WoG corpus tests), add the MODLOG entry in
   both languages, commit, push. Then delete this file and `tools/handoff/`.

RU: всё переведено и собрано; строки-переключатели языка, автоматическая сверка кода/чисел и запись в MODLOG
сделаны 08.10.2026; осталось проверить 6 переводов (WoG 03–08) и общая проверка согласованности — шаги 1–2 выше.

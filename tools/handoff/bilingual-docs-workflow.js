export const meta = {
  name: 'bilingual-docs',
  description: 'Make English the primary documentation language with synchronized Russian twins (*.ru.md), translate code strings, data and scripts',
  phases: [
    { title: 'Translate', detail: 'one agent per document: Russian twin -> English primary file' },
    { title: 'Verify', detail: 'independent adversarial reviewer per document fixes the English file' },
    { title: 'Code & data', detail: 'receiver notes in English + Russian doc generation, JSON data, scripts' },
    { title: 'Final review', detail: 'cross-document consistency critic' },
  ],
}

const ROOT = '/home/user/ModsClaudeVelz'

const GLOSSARY = `Glossary (use consistently):
ресивер -> receiver; триггер -> trigger; секция (триггера) -> (trigger) section; команда -> command; подкоманда -> subcommand;
быстрые переменные (f..t) -> quick variables (f..t); локальные переменные -> local variables; именованные/ассоциативные переменные -> named (associative) variables;
событие -> event; функция ERM -> ERM function; препроцессор -> preprocessor; интерпретатор/рантайм -> interpreter/runtime; парсер -> parser; разбор -> parsing;
командиры -> commanders; опыт стеков/отрядов -> stack experience; ранг -> rank; WoG Options stays; вогификация -> wogification;
эталонный (headless) движок -> headless reference engine; ядро -> core; адаптер -> adapter; оверлей -> overlay; плагин -> plugin; загрузчик -> loader;
сейв/сохранение -> save/saved game; символы (игры) -> (game) symbols; проверено в игре -> verified in game; не проверено -> not verified;
матрица совместимости -> compatibility matrix; отчёт совместимости -> compatibility report; заглушка -> placeholder; перекраска -> recolor;
Готово + тесты -> Done + tests; Спроектировано -> Designed; Не начато -> Not started; Полный/Частичный (RE) -> Full/Partial; н/д -> n/a; нет -> no; да -> yes;
вертикальный срез -> vertical slice; оракул -> oracle; первоисточник -> primary source; журнал (MODLOG) -> log;
клон юнита -> unit clone; бафф -> buff; первичные навыки -> primary skills; вторичные навыки -> secondary skills; артефакт -> artifact; герой -> hero; город -> town; постройка -> building;
корпус скриптов -> script corpus; инструкция (!#) -> instruction (!#); пост-инструкции -> post-instructions; метка -> label; константа -> constant; массив -> array.
Status words FULLY SUPPORTED, PARTIALLY SUPPORTED, EMULATED, WORKAROUND, UNSUPPORTED and verification tags [V-code], [V-data], [V-community], [UNVERIFIED] stay exactly as they are.`

const DOC_RULES = `Rules:
- Faithful, complete technical translation. Every sentence, list item, table row, caveat and number must be present. Do not summarize, merge, reorder, add facts or drop content. Keep heading levels and order.
- Inside inline code and code blocks keep everything byte-for-byte (identifiers, paths, ERM syntax, numbers, hex, versions, commit hashes, URLs), EXCEPT Russian prose comments inside code blocks (e.g. "# комментарий" in a shell block), which you translate.
- The first line of the Russian file is a language switch ("[English](...) | **Русский**"): drop it and do NOT add any language-switch line yourself (it is added automatically afterwards).
- References to other documents: the Russian file points to "*.ru.md"; in English use the plain name (e.g. "MODLOG.ru.md" -> "MODLOG.md", "02_ERA_Semantics.ru.md" -> "02_ERA_Semantics.md"). CLAUDE.md stays CLAUDE.md.
- Tables: same columns and rows, cells translated, valid Markdown (keep escaped "\\|" inside cells).
- Direct quotes of the user's words: translate into English and keep the original Russian in parentheses right after, e.g. "Write the documentation in Russian" («Пиши документацию на русском»).
- Russian mod captions/names quoted as data (e.g. "Во Имя Богов") stay as they are, add an English gloss in parentheses if helpful.
- Natural, precise US English for engineers. Keep Markdown formatting (bold, lists, emphasis).
- Wherever the text says the documentation is written in Russian (as a rule), state the new rule instead: documentation is English-primary (X.md) with a synchronized Russian copy next to it (X.ru.md); code comments are English.
${GLOSSARY}`

const T_SCHEMA = {
  type: 'object',
  properties: {
    file: { type: 'string' },
    notes: { type: 'string', description: 'anything uncertain or deliberately adapted' },
  },
  required: ['file', 'notes'],
}

const V_SCHEMA = {
  type: 'object',
  properties: {
    file: { type: 'string' },
    issues: {
      type: 'array',
      items: {
        type: 'object',
        properties: {
          kind: { type: 'string', description: 'omission | addition | mistranslation | code-or-number-changed | table | leftover-russian | doc-reference | terminology | other' },
          where: { type: 'string' },
          detail: { type: 'string' },
          fixed: { type: 'boolean' },
        },
        required: ['kind', 'where', 'detail', 'fixed'],
      },
    },
    verdict: { type: 'string', description: 'faithful | fixed | still-problematic' },
  },
  required: ['file', 'issues', 'verdict'],
}

function special(d) {
  if (d.en === 'CLAUDE.md') return `
Special for CLAUDE.md (instructions Claude Code reads in this repo):
- Replace the rule "Документация — на русском (все .md). Комментарии в коде — на английском" with: "Documentation: English is primary (X.md); a Russian copy lives next to it (X.ru.md) and must be kept in sync — update both in the same change. Code comments are English. Generated tables (Compatibility/ERM_Compatibility*.md and their .ru.md twins) come from 'WoG.ErmTool compat [--era] [--lang ru]'."
- Also apply the same rule change to CLAUDE.ru.md (edit only that rule bullet there, in Russian: «Документация: основная — на английском (X.md), рядом русская копия (X.ru.md); обновлять обе в одном изменении. Комментарии в коде — на английском. Сгенерированные таблицы (Compatibility/ERM_Compatibility*.md и их .ru.md) — из 'WoG.ErmTool compat [--era] [--lang ru]'.»). In the Commands section of BOTH files, the compat example should read: dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md, and ... compat --era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md.`
  if (d.en === 'HANDOFF.md') return `
Special for HANDOFF.md: it contains a ready-to-paste first prompt for the user's local Claude Code (in Russian). In the English file translate that prompt into English, and replace its last sentence "Документацию пиши на русском." with "Write documentation in English with a synchronized Russian copy (*.ru.md)." Also edit HANDOFF.ru.md: in the same prompt replace «Документацию пиши на русском.» with «Документацию пиши на английском, с русской копией рядом (*.ru.md).» (change nothing else in HANDOFF.ru.md).`
  if (d.en === 'README.md') return `
Special for README.md: right below the title paragraph add one line: "Russian version: [README.ru.md](README.ru.md). Every document in this repository has a Russian twin next to it (*.ru.md)." Also add the equivalent line to README.ru.md below its title paragraph: «Английская (основная) версия: [README.md](README.md). У каждого документа в репозитории есть английский оригинал X.md и русская копия X.ru.md.». In BOTH files the compat lines in the build section must be:
dotnet run --project tools/WoG.ErmTool -- compat --era > Compatibility/ERM_Compatibility_ERA.md        (and a line with "--era --lang ru > Compatibility/ERM_Compatibility_ERA.ru.md")
dotnet run --project tools/WoG.ErmTool -- compat > Compatibility/ERM_Compatibility.md                  (and a line with "--lang ru > Compatibility/ERM_Compatibility.ru.md")
with comments in the file's language. In the documentation map of both files, mention the two languages.`
  return ''
}

function translatePrompt(d) {
  return `Repository root: ${ROOT}. Translate the Russian Markdown document ${d.ru} into English and write the full result to ${d.en} with the Write tool (overwrite the whole file; it currently still holds the old Russian text, which is identical to ${d.ru} apart from the switch line and .ru.md references).
${DOC_RULES}
${special(d)}
Read the whole Russian file first. Return {file, notes}.`
}

function verifyPrompt(d) {
  return `Repository root: ${ROOT}. You are an independent, adversarial reviewer of a Russian -> English technical translation.
Russian source: ${d.ru} (ignore its first line, the language switch${d.en === 'CLAUDE.md' || d.en === 'HANDOFF.md' || d.en === 'README.md' ? ', and the intentionally changed documentation-language rule / pasted prompt sentence / language note' : ''}).
English translation: ${d.en}.
Compare them section by section, sentence by sentence, table row by row. Look hard for: omitted or added content; mistranslations that change technical meaning (numbers, ranges, conditions, negations, who does what, ids, versions, 'not yet' vs 'never'); code spans, paths, identifiers or numbers that changed; broken Markdown tables (column count per row); leftover Russian (Cyrillic) text in the English file other than deliberate original quotes in parentheses or quoted Russian data; references to *.ru.md left in the English file; terminology inconsistent with the glossary below; any language-switch line at the top (there must be none — remove it).
Fix every real problem directly in ${d.en} with the Edit tool. Do not rewrite text that is correct; do not touch ${d.ru}${d.en === 'CLAUDE.md' || d.en === 'HANDOFF.md' || d.en === 'README.md' ? ' except to check that the special edits described below were applied there as well (apply them if missing)' : ''}.
${d.en === 'CLAUDE.md' || d.en === 'HANDOFF.md' || d.en === 'README.md' ? 'Special edits that are intentional:' + special(d) : ''}
${GLOSSARY}
Return {file, issues:[{kind, where, detail, fixed}], verdict}.`
}

const CODE_PROMPT = `Repository root: ${ROOT} (C#, .NET; dotnet is at /root/.dotnet — run: export PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet).
Goal: documentation becomes English-primary with Russian twins (*.ru.md). The generated ERM support tables come from code, and their notes are currently Russian strings in code. Do this:
1. In src/WoG.Erm/Receivers/*.cs (CoreReceivers.cs, GameReceivers.cs, ModuleReceivers.cs, EraReceivers.cs, EraApi.cs, ReceiverRegistry.cs) and anywhere else under src/ (grep for Cyrillic), translate every Russian string literal (Declare(...) notes, UnsupportedReceiver reasons, ErmUnsupportedException messages) into concise English. Do not change behavior. Leave tests/WoG.Tests/SaveAndOptionsTests.cs "привет, мир" alone (intentional Unicode test data).
2. Keep the exact current Russian texts for documentation: in ReceiverRegistry.cs add a static dictionary English note -> Russian note (one entry per distinct note/reason, the Russian value is the current Russian literal). Change ToMarkdown() to ToMarkdown(string lang = "en"): "en" prints headers "| Receiver | Implemented | Commands and status |" and yes/no, "ru" prints the current Russian headers ("| Ресивер | Реализован | Команды и статус |", да/нет) and Russian notes (fallback to English if a note has no translation). Status words (FULLY SUPPORTED etc.) are the same in both languages. Add a unit test that every note/reason declared by the default and the Era registries has a Russian translation.
3. tools/WoG.ErmTool/Program.cs: make "compat" accept --era and --lang ru (default en) and print the WHOLE generated file: first line language switch ("**English** | [Русский](ERM_Compatibility.ru.md)" for en, "[English](ERM_Compatibility.md) | **Русский**" for ru; with _ERA in the names for --era), blank line, then the title and introduction text, then the table. Take the introduction texts from the current files Compatibility/ERM_Compatibility.md (WoG, Russian today) and Compatibility/ERM_Compatibility_ERA.md (ERA, Russian today): the Russian intro stays word-for-word for --lang ru (but mentions of other docs point to their .ru.md twins, e.g. WoG_ReverseEngineering/03_ERM_Receivers.ru.md, ERM_Compatibility.ru.md, except CLAUDE.md), and you translate it faithfully into English for en (plain .md references). The regeneration commands quoted in the intros must become: "dotnet run --project tools/WoG.ErmTool -- compat [--era] [--lang ru]". Update the usage comment at the top of Program.cs.
4. Regenerate all four files: Compatibility/ERM_Compatibility.md, Compatibility/ERM_Compatibility.ru.md, Compatibility/ERM_Compatibility_ERA.md, Compatibility/ERM_Compatibility_ERA.ru.md (redirect tool stdout; build output must not leak into the files — run the built dll: dotnet tools/WoG.ErmTool/bin/Release/net8.0/WoG.ErmTool.dll after "dotnet build tools/WoG.ErmTool -c Release -v q").
5. Build the whole solution with no warnings (dotnet build WoGOldenEra.sln) and run: dotnet test tests/WoG.Tests --filter "FullyQualifiedName!~RepositoryDataTests" (another agent is editing Compatibility/options-defaults.json concurrently; skip that test class). Also run the ERA corpus: ERA_MODS_DIR=/home/user/research/era-eng/Mods dotnet test tests/WoG.Tests --filter EraCorpus. All must pass.
Do not edit any *.md file other than the four generated ones. Do not commit. Return a short summary: files changed, number of notes translated, test results.`

const JSON_PROMPT = `Repository root: ${ROOT}. Documentation/data becomes English-primary. Edit only these two data files (do NOT build or run tests; another agent is building concurrently):
1. Compatibility/options-defaults.json — an array of WoG Options entries {index, name, description, default, defaultVerified, systems}. name/description/systems are Russian today. Make them English: for "name", prefer the official English WoG/ERA option title when you can find it in the reference material under /home/user/research (e.g. WoG option texts in /home/user/research/era-eng/Mods/WoG/lang or Data, ERA Scripts Lang/en JSON files, /home/user/research/wogify, /home/user/research/wog — grep for the option text or index; keep the meaning of the current Russian name, including markers such as "(инверт.)" -> "(inverted)"). Otherwise translate faithfully. Add a new field "nameRu" holding the current Russian name verbatim, right after "name". Translate "description" and the "systems" tags (e.g. города -> towns, бой -> battle, армии -> armies, командиры -> commanders, жилища -> dwellings ...; keep tags short, lowercase, consistent across entries). Keep index/default/defaultVerified untouched, keep the order, keep valid JSON with the same indentation style. The loader (src/WoG.Core/Options/WoGOptionCatalog.cs) ignores unknown fields — check that it does (System.Text.Json default) and report if not.
2. Compatibility/id-maps/creature.json — translate the Russian "note" values into English (e.g. "не отображено" -> "not mapped"); change nothing else.
Write atomically (write the full file content in one Write call). Validate with: python3 -c "import json;json.load(open('...'))". Return a summary with counts and which names came from official sources.`

const SCRIPTS_PROMPT = `Repository root: ${ROOT}. Make these user-run scripts English-primary:
- tools/oe-recon/collect.ps1, tools/fetch-references/fetch-references.ps1, tools/fetch-references/fetch-references.sh.
For each: the header/usage comment becomes English first, followed by the same text in Russian (a short "RU:" block) so Russian users still understand it; all runtime messages printed to the user (Say/Write-Host/echo) become English; keep behavior identical (paths, switches, logic). PowerShell files MUST stay UTF-8 with BOM and CRLF line endings (Windows PowerShell 5.1 misreads Cyrillic without a BOM): after editing, rewrite them with python3 using encoding 'utf-8-sig' and newline='\\r\\n', and confirm the first bytes are EF BB BF. Validate PowerShell syntax with: export PATH=/root/.dotnet:$PATH DOTNET_ROOT=/root/.dotnet; /tmp/claude-0/-home-user-ModsClaudeVelz/36380ad3-bb9b-50f0-8ab0-b1df00911930/scratchpad/pwsh/pwsh -NoProfile -Command '$e=$null;$t=$null;[void][System.Management.Automation.Language.Parser]::ParseFile("<file>",[ref]$t,[ref]$e); $e.Count' (must print 0), and bash -n for the .sh. Do not edit other files. Return a short summary.`

const CRITIC_SCHEMA = {
  type: 'object',
  properties: {
    problems: {
      type: 'array',
      items: {
        type: 'object',
        properties: { file: { type: 'string' }, detail: { type: 'string' }, fixed: { type: 'boolean' } },
        required: ['file', 'detail', 'fixed'],
      },
    },
    summary: { type: 'string' },
  },
  required: ['problems', 'summary'],
}

const docs = args.docs

phase('Translate')
const docWork = pipeline(
  docs,
  d => agent(translatePrompt(d), { label: `translate:${d.en}`, phase: 'Translate', schema: T_SCHEMA }),
  (t, d) => agent(verifyPrompt(d), { label: `verify:${d.en}`, phase: 'Verify', schema: V_SCHEMA, effort: 'high' })
    .then(v => ({ doc: d.en, translateNotes: t ? t.notes : null, verify: v })),
)

const side = parallel([
  () => agent(CODE_PROMPT, { label: 'code:receiver-notes+generator', phase: 'Code & data' }),
  () => agent(JSON_PROMPT, { label: 'data:json', phase: 'Code & data' }),
  () => agent(SCRIPTS_PROMPT, { label: 'scripts', phase: 'Code & data' }),
])

const [docResults, sideResults] = await Promise.all([docWork, side])
const missing = docs.filter((d, i) => !docResults[i])
if (missing.length) log(`documents without a result (retry needed): ${missing.map(d => d.en).join(', ')}`)

phase('Final review')
const critic = await agent(`Repository root: ${ROOT}. The documentation was just converted to English-primary files (X.md) with synchronized Russian twins (X.ru.md); code notes, generated tables (Compatibility/ERM_Compatibility*.md + .ru.md, produced by tools/WoG.ErmTool compat), JSON data and scripts were updated too.
Act as a completeness and consistency critic across the whole repository (do not run dotnet). Check and FIX directly:
1. Every tracked doc X.md (see: git ls-files '*.md', excluding .claude/skills/mod-any-game/references and SKILL.md) has a twin X.ru.md and vice versa; English files contain no Cyrillic except deliberate original quotes in parentheses or quoted Russian data; English files reference plain .md names, Russian twins reference .ru.md (CLAUDE.md is always CLAUDE.md).
2. Terminology is consistent across the English documents (receiver, trigger, quick variables, stack experience, commanders, headless reference engine, Done + tests / Designed / Not started, verified in game ...). Unify where documents disagree.
3. Compatibility_Matrix.md column headers and status cells use the same English wording as Compatibility_Matrix.ru.md structure (same rows/columns).
4. README.md / README.ru.md, CLAUDE.md / CLAUDE.ru.md, HANDOFF.md / HANDOFF.ru.md describe the bilingual convention and the compat commands with --lang ru consistently.
5. Generated files Compatibility/ERM_Compatibility*.md start with the language switch and are consistent with the Program.cs generator (do not hand-edit tables; if something is wrong in a generated file, report it, fixed=false).
Do NOT add language-switch lines to hand-written docs (they are added later by a script) — except generated tables, which already have them.
Return {problems:[{file, detail, fixed}], summary}.`, { label: 'critic', phase: 'Final review', schema: CRITIC_SCHEMA, effort: 'high' })

return { docResults, sideResults, critic, missing: missing.map(d => d.en) }

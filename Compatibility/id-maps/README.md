# id mapping tables (WoG/H3 ↔ Olden Era)

The plugin reads `*.json` from `BepInEx/config/WoG/id-maps/`; the file name = the domain (`creature`, `hero`,
`artifact`, `spell`, `skill`, `resource`). Row format: `{"wog": number in H3/WoG, "engine": "sid in Olden Era" | null, …}`.
Rows with `engine: null` are not mapped — ERM commands with such ids report *unsupported*, and nothing is substituted.

* `creature.json` — 150 H3 SoD creatures (0…149) with faction and level (numbers and keys come from the VCMI
  configuration, S6). WoG creatures 150–196 (8th level, commanders 174–191, etc.) will be added together with the
  unit clones in the overlay. The `visual` field is the visual key under the asset policy (existing OE model →
  recolor → …).
* Choosing `engine` (which Olden Era unit represents an H3 creature) is a design decision that has to be made with
  the creature's role and level in mind; until then the rows are left empty so that guesses are not passed off as
  a mapping.

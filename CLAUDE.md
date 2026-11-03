# My Troops' Gear

Bannerlord 1.4.8 (+ War Sails) mod: edit the battle gear of base troops (e.g. Battanian Wildling) per vanilla battle
set and slot.
Only troops of MY parties wear the edited version - main party, parties led by player-clan heroes, and the
garrison/militia of player-clan settlements. Everyone else on the map keeps vanilla. A lighter, save-safe
replacement for My Little Warband (no custom troop tree; copy none of MLW's code or XML - it has no license).

## Layout

- `src\MyTroopsGear.Core` - netstandard2.0, no game references: the override model (`GearSlots`, `SetOverride` =
  one set's slots, `TroopOverride` = set index -> SetOverride, `OverrideStore`) and its save lines; the editor's pure
  logic (`GearEditing`: per-set SetSlot / RevertSlot with the block rules, ApplyToAllSets / CanApplyToAllSets, ResetSet,
  ResetTroop; `IsSlotEdited`: a stored slot counts only when it differs from THAT set's vanilla item, and slots / blocks
  back on that set's vanilla items are dropped (`DropUnchanged`); `SpawnSet`: which set a soldier wears;
  `ItemCandidates`: picker filters; `UnitPageView`: the unit page's Vanilla | My version rules; `PartyCardLook`: which
  party screen cards get my look). Unit-tested.
- `src\MyTroopsGear` - net472 module: `SubModule`, `GearBehavior` (save), `GearScope` (whose parties),
  `SpawnPatch` (Harmony prefix on `Mission.SpawnAgent`), `GearApplier`, `GearCommands` (console), `ModLog`.
  `UI\`: `EncyclopediaUnitPageButtons` (mixin + prefab insert on the unit page: "Vanilla | My version" toggle, shown
  only for edited troops, rebuilds the set selector from override-applied clones; session-static choice; and "Edit gear"), `GearEditorWindow`
  (layer 320, above the Encyclopedia's 310; Esc closes), `GearEditorVM` + `GearEditorParts` (arrows = the set being
  edited, "Set i / n"; rows show and edit that set; per row Change / Reset / All sets; "Reset this set" (1 click) and
  "Reset whole unit to vanilla" (2 clicks)), `ItemCatalog`,
  `EncyclopediaBackGuard` (holds the Encyclopedia's Backspace-back while the editor is open), `PartyScreenPatch`
  (postfix on `PartyCharacterVM.GetCharacterCode`: troop cards + upgrade previews of my side's members show the
  edited look from a cloned set 0 with set 0's edits - one stable look; prisoners, heroes and the other party stay
  vanilla; no cache; plus a postfix on `PartyVM.RefreshCurrentCharacterInformation` for the big selected-troop model,
  same rule). The unit page's My version shows each set i with set i's edits. "Edit gear *" and the toggle show when
  any set has a real edit.
- `tests\MyTroopsGear.Core.Tests` - xunit, net8.0.
- `module\` - SubModule.xml, `GUI\Prefabs\MyTroopsGearEditor.xml`. Build output lands in `module\bin` (git-ignored).
- Machine paths: `Directory.Build.props.user` (git-ignored) sets `GameFolder`, `HarmonyBinFolder`, `UIExtenderExBinFolder`.

## Commands

```
dotnet build src\MyTroopsGear -c Release
dotnet test tests\MyTroopsGear.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\check-gui.ps1  # after a build: prefabs + prefab extensions vs game widgets/brushes/sprites/bindings
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1     # builds + installs Modules\MyTroopsGear.Dev, "(dev)"
powershell -ExecutionPolicy Bypass -File tools\package.ps1    # release: dist\MyTroopsGear (Workshop folder) + dist\MyTroopsGear_v<ver>.zip; never touches the game
python tools\make_thumbnail.py                                 # docs\thumbnail.jpg (1024x1024) from docs\screenshots
```

Release: version in `module\SubModule.xml` (Id `MyTroopsGear`; only deploy.ps1 re-badges it `.Dev`). Store texts:
`tools\NEXUS-DESCRIPTION.bbcode`, `tools\STEAM-DESCRIPTION.bbcode`; Steam upload steps in `tools\WORKSHOP-UPLOAD.md`.
Never install the non-dev module into the game's Modules (it would load beside `MyTroopsGear.Dev`).

deploy.ps1 refuses if the DLLs are locked (game running). Never launch, restart or kill the game without asking Toni.
Mod log: `Documents\Mount and Blade II Bannerlord\Configs\MyTroopsGear\my_troops_gear.log`.
Console (Ctrl+~ in campaign; set numbers are 1-based like the editor): `mytroopsgear.set <troopId> <set|all> <slot> <itemId|empty|vanilla>`,
`mytroopsgear.clear [troopId [set]]`, `mytroopsgear.list [troopId]`.

## Design rules

- **Never mutate a CharacterObject's equipment** (BattleEquipments, FirstBattleEquipment, the roster). Troop
  Equipment Upgrade and Culture Equipment Seeder read those to price upgrades and seed markets, and every AI
  party uses them. Overrides are applied only to fresh copies: the spawn roll, and (later) cloned sets for UI.
- Overrides are per troop, per vanilla battle set (index into `troop.BattleEquipments` in game order, NOT the
  Encyclopedia's de-duplicated list; the editor uses the same raw list), per battle slot (EquipmentIndex 0-3, 5-11;
  never 4, the banner). Absent slot = that set's vanilla item. `empty` = slot emptied on purpose. Weapons 0-3 are one
  block and horse + harness one pair: if any slot of a block is overridden, the whole block is stored (seeded from
  that set's vanilla items) and applied. Civilian gear stays vanilla.
- **How a soldier gets his set.** Vanilla 1.4.8 does not roll one set: `Equipment.GetRandomEquipmentElements` with the
  agent's seed draws a fresh set number for EVERY slot (`new Random(seed)`, three draws per slot), so a vanilla soldier
  is a per-slot mix of the troop's sets. Per-set edits need one whole set, so `SpawnSet.Pick` takes the first number
  vanilla itself draws (`new Random(seed).Next() % count`; unseeded: `MBRandom.RandomInt`): same seed, same 1/count
  chance per set, stable per soldier. If that set has edits, the soldier wears that set whole - its vanilla items with
  vanilla's random modifiers, the set's edits on top (no modifier). If it has none, the hook steps aside and vanilla
  rolls its usual mix. So an edited troop loses the cross-set mix only for soldiers that drew an edited set.
- Spawn hook only acts when nothing else chose the gear (`AgentOverridenSpawnEquipment == null`, not fixed, not
  civilian), so tournaments, arena and practice fights stay untouched. It must never throw into the mission:
  try/catch + `ModLog.ErrorOnce`.
- Autoresolve ignores gear; overrides only matter in missions.
- **Patch nothing at module load.** Harmony JIT-compiles the method it patches; a game UI method that reads a static
  field of a helper class (e.g. `CampaignUIHelper.SkillObjectComparerInstance`) runs that class's static constructor
  right then, and before a Game exists `GameTexts.FindText` throws inside it - the type stays broken for the whole run
  and the campaign map crashes on load (happened 2026-11-03 with `[ViewModelMixin("RefreshValues")]`, which makes
  UIExtenderEx patch `RefreshValues` in OnSubModuleLoad). So: `[ViewModelMixin]` without a refresh method name
  (check-gui fails otherwise), and all Harmony patches in `OnGameStart`, each in its own try/catch with a log line.

## Save format

One `List<string>` under the key `MyTroopsGear_overrides_v2` in `GearBehavior.SyncData`, one line per slot:
`troopId|set|slot|itemId`, set 0-based (e.g. `battanian_wildling|1|2|empty`). Parsing is tolerant: bad lines are
skipped, partial blocks completed with `empty`. Older saves have `MyTroopsGear_overrides_v1` with `troopId|slot|itemId`
lines (one edit for every set): read on load only, kept as "all sets" edits, and on session launch (`Prune`) copied
onto every set the troop has (a set's own line wins), then written as v2 lines; if that never ran they are written
back in the old 3-field form, so nothing is lost. On session launch, unknown troops are dropped, sets the troop no
longer has (set count shrank) are dropped, an unknown item reverts its block of that set to vanilla, and stored slots
equal to their set's vanilla item are dropped. **No SaveableTypeDefiner, no custom saved types** - the mod must stay
removable mid-campaign. If the format must change again, add a new key (`_v3`) and read the old ones.

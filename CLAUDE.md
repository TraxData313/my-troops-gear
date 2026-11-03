# My Troops' Gear

Bannerlord 1.4.8 (+ War Sails) mod: edit the battle gear of base troops (e.g. Battanian Wildling) per slot.
Only troops of MY parties wear the edited version - main party, parties led by player-clan heroes, and the
garrison/militia of player-clan settlements. Everyone else on the map keeps vanilla. A lighter, save-safe
replacement for My Little Warband (no custom troop tree; copy none of MLW's code or XML - it has no license).

## Layout

- `src\MyTroopsGear.Core` - netstandard2.0, no game references: the override model (`GearSlots`,
  `TroopOverride`, `OverrideStore`) and its save lines; the editor's pure logic (`GearEditing`: per-slot
  revert with the block rules; `IsSlotEdited`: a weapon/mount block mate counts as edited only when it differs from the
  vanilla first-set item, and a block back on all-vanilla items is dropped; `ItemCandidates`: picker filters; `UnitPageView`: the unit page's
  Vanilla | My version rules; `PartyCardLook`: which party screen cards get my look). Unit-tested.
- `src\MyTroopsGear` - net472 module: `SubModule`, `GearBehavior` (save), `GearScope` (whose parties),
  `SpawnPatch` (Harmony prefix on `Mission.SpawnAgent`), `GearApplier`, `GearCommands` (console), `ModLog`.
  `UI\`: `EncyclopediaUnitPageButtons` (mixin + prefab insert on the unit page: "Vanilla | My version" toggle, shown
  only for edited troops, rebuilds the set selector from override-applied clones; session-static choice; and "Edit gear"), `GearEditorWindow`
  (layer 320, above the Encyclopedia's 310; Esc closes), `GearEditorVM` + `GearEditorParts`, `ItemCatalog`,
  `EncyclopediaBackGuard` (holds the Encyclopedia's Backspace-back while the editor is open), `PartyScreenPatch`
  (postfix on `PartyCharacterVM.GetCharacterCode`: troop cards + upgrade previews of my side's members show the
  edited look from a cloned first battle set; prisoners, heroes and the other party stay vanilla; no cache; plus a
  postfix on `PartyVM.RefreshCurrentCharacterInformation` for the big selected-troop model, same rule).
- `tests\MyTroopsGear.Core.Tests` - xunit, net8.0.
- `module\` - SubModule.xml, `GUI\Prefabs\MyTroopsGearEditor.xml`. Build output lands in `module\bin` (git-ignored).
- Machine paths: `Directory.Build.props.user` (git-ignored) sets `GameFolder`, `HarmonyBinFolder`, `UIExtenderExBinFolder`.

## Commands

```
dotnet build src\MyTroopsGear -c Release
dotnet test tests\MyTroopsGear.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\check-gui.ps1  # after a build: prefabs + prefab extensions vs game widgets/brushes/sprites/bindings
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1     # builds + installs Modules\MyTroopsGear.Dev, "(dev)"
```

deploy.ps1 refuses if the DLLs are locked (game running). Never launch, restart or kill the game without asking Toni.
Mod log: `Documents\Mount and Blade II Bannerlord\Configs\MyTroopsGear\my_troops_gear.log`.
Console (Ctrl+~ in campaign): `mytroopsgear.set <troopId> <slot> <itemId|empty|vanilla>`, `mytroopsgear.clear [troopId]`, `mytroopsgear.list`.

## Design rules

- **Never mutate a CharacterObject's equipment** (BattleEquipments, FirstBattleEquipment, the roster). Troop
  Equipment Upgrade and Culture Equipment Seeder read those to price upgrades and seed markets, and every AI
  party uses them. Overrides are applied only to fresh copies: the spawn roll, and (later) cloned sets for UI.
- Overrides are per troop, per battle slot (EquipmentIndex 0-3, 5-11; never 4, the banner). Absent slot = vanilla
  random roll. `empty` = slot emptied on purpose. Weapons 0-3 are one block and horse + harness one pair: if any
  slot of a block is overridden, the whole block is stored and applied. Civilian gear stays vanilla.
- Spawn hook only acts when nothing else chose the gear (`AgentOverridenSpawnEquipment == null`, not fixed, not
  civilian), so tournaments, arena and practice fights stay untouched. It rolls vanilla with the same seed, then
  overwrites. It must never throw into the mission: try/catch + `ModLog.ErrorOnce`.
- Autoresolve ignores gear; overrides only matter in missions.

## Save format

One `List<string>` under the key `MyTroopsGear_overrides_v1` in `GearBehavior.SyncData`, one line per slot:
`troopId|slot|itemId` (e.g. `battanian_wildling|2|empty`). Parsing is tolerant: bad lines are skipped, partial
blocks completed with `empty`. On session launch, unknown troops are dropped, and an unknown item reverts its
block to vanilla. **No SaveableTypeDefiner, no custom saved types** - the mod must stay removable mid-campaign.
If the format must change, add a new key (`_v2`) and read the old one.

# My Troops' Gear — technical notes

For Bannerlord v1.4.8, with or without the War Sails DLC.

## Using it

Open a troop's page in the Encyclopedia (from anywhere, the party screen included) and press **Edit gear**. The
editor shows one of the troop's vanilla battle sets at a time; the arrows under the model step through them
("Set 1 / 4"). Each slot row (four weapons, head, shoulders, body, arms, legs, horse, harness) has:

- **Change** - opens the picker: items with tier and price, filtered by tier, culture (the troop's culture plus
  neutral by default) and type, or searched by name. "Empty" leaves the slot empty on purpose.
- **Reset** - back to that set's vanilla item (shown only on edited slots).
- **All sets** - puts this slot's item on every set of the troop.

**Reset this set** clears one set; **Reset whole unit to vanilla** (click twice) clears the troop.
"Edit gear *" marks a troop with edits, and its page gets a **Vanilla | My version** toggle.

Console (Ctrl+~ in a campaign; set numbers count from 1, as the editor shows them):

```
mytroopsgear.set <troopId> <set|all> <slot> <itemId|empty|vanilla>
mytroopsgear.clear [troopId [set]]
mytroopsgear.list [troopId]
```

## Who wears the edits

Soldiers of your main party, of parties led by heroes of your clan, and the garrison and militia of your clan's
settlements. Everyone else - other lords, bandits, caravans, your prisoners - wears vanilla gear.

## How it works

- The troop's own equipment is **never changed**. Troop Equipment Upgrade, markets and every AI party read the
  vanilla troop. The edits are applied only to a copy: when a soldier spawns in a mission (a Harmony prefix on
  `Mission.SpawnAgent`), and to cloned sets for the party screen and the Encyclopedia.
- Vanilla mixes a soldier's gear slot by slot across the troop's sets. A soldier who draws an edited set wears
  that set whole, edits on top; one who draws an untouched set keeps the vanilla mix. Same odds per set as vanilla.
- The four weapon slots are one block, horse and harness one pair: editing one stores the whole block.
- Tournaments, the arena, practice fights and anything that already chose a soldier's gear are left alone.
- The party screen shows set 1 with its edits for your troops (cards, upgrade previews, the big model).
- Civilian gear and the banner slot are never edited.

## Save

One list of text lines in the campaign save, `troopId|set|slot|itemId`. No custom saved types, so the mod can be
added or removed at any time. On load, unknown troops, sets and items are dropped quietly (their slots go back to vanilla).

## Limits

- Auto-resolve does not look at gear, so edits only matter in battles you fight.
- Formations come from the troop, not the gear: a spearman given a bow or a horse still lines up as infantry.
- A mod that rewrites the same troops (My Little Warband and the like) would fight with this one.

## Building

Needs the .NET SDK and the game, Harmony and UIExtenderEx installed. Paths are in `Directory.Build.props`
(override them in a git-ignored `Directory.Build.props.user`).

```
dotnet build src\MyTroopsGear -c Release
dotnet test tests\MyTroopsGear.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File tools\check-gui.ps1   # prefabs vs the game's widgets, brushes, sprites, bindings
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1      # installs Modules\MyTroopsGear.Dev, "(dev)" in the launcher
powershell -ExecutionPolicy Bypass -File tools\package.ps1     # dist\MyTroopsGear + dist\MyTroopsGear_v<version>.zip
```

# My Troops' Gear

A Mount & Blade II: Bannerlord (1.4.8, War Sails compatible) mod for editing the gear of your own troops.

Pick a base troop - say the Battanian Wildling - and change its weapons, armour or horse slot by slot.
Only troops in **your** parties use your version: your party, your clan's parties and your garrisons.
Everyone else in Calradia keeps the vanilla gear.

- A troop has several vanilla gear sets (one Volunteer carries a hatchet, another a spear). You edit each set
  on its own, or press "All sets" on a slot to put that item on every set. Each soldier wears one set, edited.
- Sets you don't touch keep the game's random variety.
- Edits are free and instant; Troop Equipment Upgrade keeps working as before (it still sees the vanilla troop).
- Save-safe: the edits are stored as plain text lines, so the mod can be added or removed mid-campaign.
- Gear matters in battles (field, siege, hideout, naval); auto-resolve does not look at gear.

**Status: work in progress.** Open the editor with "Edit gear" on an Encyclopedia unit page. Debug console
commands (set numbers count from 1, as the editor shows them):

```
mytroopsgear.set <troopId> <set|all> <slot> <itemId|empty|vanilla>
mytroopsgear.clear [troopId [set]]
mytroopsgear.list [troopId]
```

Requires Harmony and UIExtenderEx.

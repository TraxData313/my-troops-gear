# My Troops' Gear

A Mount & Blade II: Bannerlord (1.4.8, War Sails compatible) mod for editing the gear of your own troops.

Pick a base troop - say the Battanian Wildling - and change its weapons, armour or horse slot by slot.
Only troops in **your** parties use your version: your party, your clan's parties and your garrisons.
Everyone else in Calradia keeps the vanilla gear.

- Slots you don't touch keep the game's random variety.
- Edits are free and instant; Troop Equipment Upgrade keeps working as before (it still sees the vanilla troop).
- Save-safe: the edits are stored as plain text lines, so the mod can be added or removed mid-campaign.
- Gear matters in battles (field, siege, hideout, naval); auto-resolve does not look at gear.

**Status: early work in progress.** The editor window (from the Encyclopedia unit page) is not built yet;
for now edits are made with debug console commands:

```
mytroopsgear.set <troopId> <slot> <itemId|empty|vanilla>
mytroopsgear.clear [troopId]
mytroopsgear.list
```

Requires Harmony and UIExtenderEx.

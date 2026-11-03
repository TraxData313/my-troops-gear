using System;
using System.Collections.Generic;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// The editor's actions on the store, with the block rules applied the way a player expects them. Everything
    /// works on ONE vanilla battle set of the troop (index 0-based); <c>vanilla</c> lookups give that set's item id
    /// per slot (null = empty), <c>vanillaOf</c> lookups take (set, slot).
    ///
    /// The one rule for "edited": a stored slot counts only when its item differs from that set's vanilla item.
    /// A stored block mate that still holds its vanilla item is just carried along with its block; an armour piece
    /// or a whole block back on vanilla items is dropped, so the set is worn exactly as vanilla again.
    /// </summary>
    public static class GearEditing
    {
        /// <summary>
        /// Puts one slot of one set back to vanilla. An armour slot simply loses its override. A slot of the weapon
        /// block or the mount pair is set to the set's vanilla item and the rest of the block stays as edited; once
        /// every slot of the block is back on its vanilla item, the block is dropped. False when the slot was not stored.
        /// </summary>
        public static bool RevertSlot(OverrideStore store, string troopId, int set, int slot, Func<int, string?> vanilla)
        {
            var over = store.GetSet(troopId, set);
            if (over == null || !over.IsOverridden(slot))
                return false;
            if (GearSlots.BlockOf(slot).Count == 1)
            {
                store.Revert(troopId, set, slot);
                return true;
            }
            store.Set(troopId, set, slot, VanillaOrEmpty(vanilla, slot), vanilla);
            DropUnchanged(store, troopId, set, vanilla);
            return true;
        }

        /// <summary>
        /// The picker's write: <paramref name="itemId"/> into <paramref name="slot"/> of <paramref name="set"/>. Picking
        /// the set's own vanilla item for an armour slot reverts that slot; a weapon block / mount pair that ends up on
        /// exactly the set's vanilla items is dropped.
        /// </summary>
        public static void SetSlot(OverrideStore store, string troopId, int set, int slot, string itemId, Func<int, string?> vanilla)
        {
            if (GearSlots.BlockOf(slot).Count == 1 && itemId == VanillaOrEmpty(vanilla, slot))
            {
                store.Revert(troopId, set, slot);
                return;
            }
            store.Set(troopId, set, slot, itemId, vanilla);
            DropUnchanged(store, troopId, set, vanilla);
        }

        /// <summary>Drops, in one set, every armour slot holding the vanilla item and every weapon block / mount pair whose
        /// slots all hold their vanilla items: they change nothing. Returns true if something was dropped.</summary>
        public static bool DropUnchanged(OverrideStore store, string troopId, int set, Func<int, string?> vanilla)
        {
            bool dropped = false;
            foreach (var block in new[] { GearSlots.WeaponBlock, GearSlots.MountPair })
            {
                var over = store.GetSet(troopId, set);
                if (over == null || !over.IsOverridden(block[0]) || !IsBlockVanilla(over, block, vanilla))
                    continue;
                store.Revert(troopId, set, block[0]);
                dropped = true;
            }
            foreach (var slot in GearSlots.All)
            {
                if (GearSlots.BlockOf(slot).Count != 1) continue;
                var over = store.GetSet(troopId, set);
                if (over == null || !over.TryGet(slot, out var id) || id != VanillaOrEmpty(vanilla, slot))
                    continue;
                store.Revert(troopId, set, slot);
                dropped = true;
            }
            return dropped;
        }

        /// <summary>Whether the player really changed <paramref name="slot"/> of this set - what the editor marks as edited
        /// (gold name, Reset, the count): stored, and different from the set's vanilla item.</summary>
        public static bool IsSlotEdited(SetOverride? over, int slot, Func<int, string?> vanilla)
        {
            if (over == null || !over.TryGet(slot, out var id))
                return false;
            return id != VanillaOrEmpty(vanilla, slot);
        }

        /// <summary>How many slots of this set <see cref="IsSlotEdited"/> counts.</summary>
        public static int EditedSlotCount(SetOverride? over, Func<int, string?> vanilla)
        {
            int n = 0;
            foreach (var s in GearSlots.All)
                if (IsSlotEdited(over, s, vanilla))
                    n++;
            return n;
        }

        public static bool HasEdits(SetOverride? over, Func<int, string?> vanilla) => EditedSlotCount(over, vanilla) > 0;

        /// <summary>Edited slots summed over every set of the troop.</summary>
        public static int EditedSlotCount(TroopOverride? over, Func<int, int, string?> vanillaOf)
        {
            if (over == null) return 0;
            int n = 0;
            foreach (var set in over.Sets)
            {
                int k = set.Key;
                n += EditedSlotCount(set.Value, s => vanillaOf(k, s));
            }
            return n;
        }

        /// <summary>Some set has a really edited slot (the unit page's "*" and its Vanilla | My version toggle).</summary>
        public static bool HasEdits(TroopOverride? over, Func<int, int, string?> vanillaOf) => EditedSlotCount(over, vanillaOf) > 0;

        /// <summary>What a set wears in <paramref name="slot"/>: the stored item, else the set's vanilla item; Empty for none.</summary>
        public static string EffectiveItem(SetOverride? over, int slot, Func<int, string?> vanilla) =>
            over != null && over.TryGet(slot, out var id) ? id : VanillaOrEmpty(vanilla, slot);

        /// <summary>The "All sets" button of a row is useful: some other set wears something else in this slot.</summary>
        public static bool CanApplyToAllSets(TroopOverride? over, int set, int slot, int setCount, Func<int, int, string?> vanillaOf)
        {
            var mine = EffectiveItem(over?.GetSet(set), slot, s => vanillaOf(set, s));
            for (int k = 0; k < setCount; k++)
            {
                if (k == set) continue;
                int kk = k;
                if (EffectiveItem(over?.GetSet(k), slot, s => vanillaOf(kk, s)) != mine)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// "All sets": puts what <paramref name="set"/> wears in <paramref name="slot"/> (edited or vanilla) onto that slot of
        /// every other set, with that set's own rules (a new weapon block keeps the set's other vanilla weapons; an item
        /// equal to that set's vanilla item simply is vanilla there). Returns how many sets changed.
        /// </summary>
        public static int ApplyToAllSets(OverrideStore store, string troopId, int set, int slot, int setCount, Func<int, int, string?> vanillaOf)
        {
            var item = EffectiveItem(store.GetSet(troopId, set), slot, s => vanillaOf(set, s));
            int changed = 0;
            for (int k = 0; k < setCount; k++)
            {
                if (k == set) continue;
                int kk = k;
                Func<int, string?> vanilla = s => vanillaOf(kk, s);
                if (EffectiveItem(store.GetSet(troopId, k), slot, vanilla) == item) continue;
                SetSlot(store, troopId, k, slot, item, vanilla);
                changed++;
            }
            return changed;
        }

        /// <summary>One set goes fully vanilla. False if it had no edits.</summary>
        public static bool ResetSet(OverrideStore store, string troopId, int set) => store.ClearSet(troopId, set);

        /// <summary>The troop goes fully vanilla, every set. False if it had no override.</summary>
        public static bool ResetTroop(OverrideStore store, string troopId) => store.Clear(troopId);

        /// <summary>"Tier 3 | 1,250" - an item's tier (1-6) and value in denars, for rows and picker cards.</summary>
        public static string TierPriceText(int tier, int value) =>
            "Tier " + tier + " | " + value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Every slot of <paramref name="block"/> holds exactly its vanilla item.</summary>
        public static bool IsBlockVanilla(SetOverride over, IReadOnlyList<int> block, Func<int, string?> vanilla)
        {
            foreach (var s in block)
            {
                if (!over.TryGet(s, out var id) || id != VanillaOrEmpty(vanilla, s))
                    return false;
            }
            return true;
        }

        private static string VanillaOrEmpty(Func<int, string?> vanilla, int slot)
        {
            var id = vanilla(slot);
            return TroopOverride.IsValidId(id) ? id! : GearSlots.Empty;
        }

        /// <summary>The editor's row label for a slot.</summary>
        public static string Label(int slot)
        {
            switch (slot)
            {
                case GearSlots.Weapon0: return "Weapon 1";
                case GearSlots.Weapon1: return "Weapon 2";
                case GearSlots.Weapon2: return "Weapon 3";
                case GearSlots.Weapon3: return "Weapon 4";
                case GearSlots.Head: return "Head";
                case GearSlots.Body: return "Body";
                case GearSlots.Gloves: return "Arms";
                case GearSlots.Leg: return "Legs";
                case GearSlots.Cape: return "Shoulders";
                case GearSlots.Horse: return "Horse";
                case GearSlots.Harness: return "Harness";
                default: return GearSlots.Name(slot);
            }
        }

        /// <summary>The editor's row order: weapons, then armour head to foot, then the mount.</summary>
        public static readonly int[] EditorOrder =
        {
            GearSlots.Weapon0, GearSlots.Weapon1, GearSlots.Weapon2, GearSlots.Weapon3,
            GearSlots.Head, GearSlots.Cape, GearSlots.Body, GearSlots.Gloves, GearSlots.Leg,
            GearSlots.Horse, GearSlots.Harness,
        };
    }

    /// <summary>
    /// Which vanilla battle set a spawning soldier wears. Vanilla (<c>Equipment.GetRandomEquipmentElements</c>, 1.4.8) does
    /// not pick one set: with the agent's seed it draws a fresh set number for EVERY slot from <c>new Random(seed)</c>, so
    /// a soldier's gear is a per-slot mix of the troop's sets. For per-set edits a soldier must wear one set whole, so we
    /// take the first number vanilla itself would draw (<c>new Random(seed).Next() % count</c>, the set of weapon slot 0):
    /// same seed, same chance per set (1 / count), and the same soldier always gets the same set.
    /// </summary>
    public static class SpawnSet
    {
        /// <param name="seed">AgentBuildData.AgentEquipmentSeed; -1 = no seed (vanilla then uses MBRandom).</param>
        /// <param name="count">The troop's battle set count.</param>
        /// <param name="unseeded">MBRandom.RandomInt(count) for the unseeded case.</param>
        /// <returns>The set index, or -1 when the troop has no set.</returns>
        public static int Pick(int seed, int count, Func<int, int> unseeded)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (seed != -1) return new Random(seed).Next() % count;
            var k = unseeded(count);
            return k < 0 || k >= count ? 0 : k;
        }
    }
}

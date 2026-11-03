using System;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// The editor's actions on the store, with the block rules applied the way a player expects them.
    /// </summary>
    public static class GearEditing
    {
        /// <summary>
        /// Puts one slot back to vanilla. An armour slot simply loses its override. A slot of the weapon block or the
        /// mount pair can not go back to the random roll on its own (the block is overridden as one), so it is set to the
        /// troop's vanilla item for that slot (<paramref name="vanilla"/>, the first battle set; null = empty) and the
        /// rest of the block stays as edited. Once every slot of the block is back on its vanilla item, the whole block
        /// is dropped, so the troop rolls its vanilla sets again. Returns false when the slot was not overridden.
        /// </summary>
        public static bool RevertSlot(OverrideStore store, string troopId, int slot, Func<int, string?> vanilla)
        {
            var over = store.Get(troopId);
            if (over == null || !over.IsOverridden(slot))
                return false;
            var block = GearSlots.BlockOf(slot);
            if (block.Count == 1)
            {
                store.Revert(troopId, slot);
                return true;
            }

            store.Set(troopId, slot, VanillaOrEmpty(vanilla, slot), vanilla);
            DropVanillaBlocks(store, troopId, vanilla);
            return true;
        }

        /// <summary>
        /// The picker's write: <paramref name="itemId"/> into <paramref name="slot"/>. If that puts the weapon block or
        /// the mount pair back on exactly the vanilla items (the player picked the troop's own item again), the block is
        /// dropped, so the troop rolls its vanilla sets again instead of being pinned to its first set.
        /// </summary>
        public static void SetSlot(OverrideStore store, string troopId, int slot, string itemId, Func<int, string?> vanilla)
        {
            store.Set(troopId, slot, itemId, vanilla);
            DropVanillaBlocks(store, troopId, vanilla);
        }

        /// <summary>Drops every stored weapon block / mount pair whose slots all hold the vanilla (first set) item:
        /// such a block changes nothing the player chose. Armour slots are kept - one is stored only when picked, and
        /// picking the first set's helmet on purpose pins it on every set. Returns true if something was dropped.</summary>
        public static bool DropVanillaBlocks(OverrideStore store, string troopId, Func<int, string?> vanilla)
        {
            bool dropped = false;
            foreach (var block in new[] { GearSlots.WeaponBlock, GearSlots.MountPair })
            {
                var over = store.Get(troopId);
                if (over == null || !over.IsOverridden(block[0]) || !IsBlockVanilla(over, block, vanilla))
                    continue;
                store.Revert(troopId, block[0]);
                dropped = true;
            }
            return dropped;
        }

        /// <summary>
        /// Whether the player really changed <paramref name="slot"/> - what the editor marks as edited (gold name, Reset,
        /// the "N slots edited" count). An armour slot is edited whenever it is stored. A slot of the weapon block or
        /// mount pair is stored together with its block mates (seeded from the first set), so it counts as edited only
        /// when its item differs from the troop's vanilla first-set item (<paramref name="vanilla"/>; null = empty).
        /// </summary>
        public static bool IsSlotEdited(TroopOverride? over, int slot, Func<int, string?> vanilla)
        {
            if (over == null || !over.TryGet(slot, out var id))
                return false;
            if (GearSlots.BlockOf(slot).Count == 1)
                return true;
            return id != VanillaOrEmpty(vanilla, slot);
        }

        /// <summary>How many slots <see cref="IsSlotEdited"/> counts.</summary>
        public static int EditedSlotCount(TroopOverride? over, Func<int, string?> vanilla)
        {
            int n = 0;
            foreach (var s in GearSlots.All)
                if (IsSlotEdited(over, s, vanilla))
                    n++;
            return n;
        }

        /// <summary>At least one slot is really edited (the unit page's "*" and its Vanilla | My version toggle).</summary>
        public static bool HasEdits(TroopOverride? over, Func<int, string?> vanilla) => EditedSlotCount(over, vanilla) > 0;

        /// <summary>
        /// The grey text of a slot that is not edited. <paramref name="pinned"/>: the slot is stored with its block, so
        /// every set now uses the first set's item. <paramref name="varies"/>: the troop's vanilla sets differ in this
        /// slot, so the shown set's item is only one of the rolls.
        /// </summary>
        public static string VanillaText(string itemName, bool pinned, bool varies)
        {
            if (pinned && varies) return "vanilla (set 1): " + itemName;
            if (varies) return "vanilla (varies): " + itemName;
            return "vanilla: " + itemName;
        }

        /// <summary>"Tier 3 | 1,250" - an item's tier (1-6) and value in denars, for rows and picker cards.</summary>
        public static string TierPriceText(int tier, int value) =>
            "Tier " + tier + " | " + value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The line under the set arrows: edits are an overlay on every vanilla set, not on the shown one.</summary>
        public static string SetsText(int setCount) =>
            setCount > 1 ? "Your edits apply to all " + setCount + " sets" : "This troop has one vanilla set";

        /// <summary>The troop goes fully vanilla. False if it had no override.</summary>
        public static bool ResetTroop(OverrideStore store, string troopId) => store.Clear(troopId);

        /// <summary>Every slot of <paramref name="block"/> holds exactly its vanilla (first set) item.</summary>
        public static bool IsBlockVanilla(TroopOverride over, System.Collections.Generic.IReadOnlyList<int> block, Func<int, string?> vanilla)
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
}

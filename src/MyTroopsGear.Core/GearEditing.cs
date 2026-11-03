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
            over = store.Get(troopId);
            if (over != null && IsBlockVanilla(over, block, vanilla))
                store.Revert(troopId, slot);
            return true;
        }

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

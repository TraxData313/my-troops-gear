using System;
using MyTroopsGear.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace MyTroopsGear
{
    /// <summary>
    /// Turns a stored override into game equipment. It only ever writes onto a fresh Equipment (a spawn roll
    /// or a clone); the troop's own CharacterObject sets are never touched - Troop Equipment Upgrade, Culture
    /// Equipment Seeder and every AI party read those.
    /// </summary>
    internal static class GearApplier
    {
        /// <summary>Core's slot numbers must be the game's EquipmentIndex values.</summary>
        public static bool SlotsMatchGame =>
            GearSlots.Weapon0 == (int)EquipmentIndex.Weapon0 && GearSlots.Weapon3 == (int)EquipmentIndex.Weapon3
            && GearSlots.Head == (int)EquipmentIndex.Head && GearSlots.Body == (int)EquipmentIndex.Body
            && GearSlots.Leg == (int)EquipmentIndex.Leg && GearSlots.Gloves == (int)EquipmentIndex.Gloves
            && GearSlots.Cape == (int)EquipmentIndex.Cape && GearSlots.Horse == (int)EquipmentIndex.Horse
            && GearSlots.Harness == (int)EquipmentIndex.HorseHarness;

        public static ItemObject? FindItem(string itemId) =>
            itemId == GearSlots.Empty ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);

        /// <summary>Writes the override's slots onto <paramref name="equipment"/>. Overridden items come
        /// without a modifier (plain quality). An id that no longer resolves leaves that slot as it was.</summary>
        public static void Apply(TroopOverride over, Equipment equipment)
        {
            foreach (var slot in over.Slots)
            {
                var index = (EquipmentIndex)slot.Key;
                if (slot.Value == GearSlots.Empty)
                {
                    equipment[index] = default(EquipmentElement);
                    continue;
                }
                var item = FindItem(slot.Value);
                if (item != null)
                    equipment[index] = new EquipmentElement(item);
            }
        }

        /// <summary>What the troop's agent spawns with: the vanilla roll exactly as Mission.SpawnAgent makes it
        /// (same seed, same random modifiers), then the overridden slots on top.</summary>
        public static Equipment RollWithOverride(CharacterObject troop, TroopOverride over, int seed)
        {
            var equipment = Equipment.GetRandomEquipmentElements(troop, !Game.Current.GameType.IsCoreOnlyGameMode,
                Equipment.EquipmentType.Battle, seed);
            Apply(over, equipment);
            return equipment;
        }

        /// <summary>The item id the troop's first vanilla battle set has in <paramref name="slot"/> (block seed
        /// for a new weapon block / mount pair), or null for an empty slot.</summary>
        public static string? VanillaItemId(CharacterObject troop, int slot)
        {
            try
            {
                return troop.FirstBattleEquipment?[(EquipmentIndex)slot].Item?.StringId;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Why <paramref name="item"/> can not go into <paramref name="slot"/>, or null if it can.</summary>
        public static string? WhyNotFits(ItemObject item, int slot)
        {
            if (item.IsCraftedByPlayer) return "player-crafted items belong to one save and are not allowed";
            if (!Equipment.IsItemFitsToSlot((EquipmentIndex)slot, item))
                return "a " + item.ItemType + " does not fit the " + GearSlots.Name(slot) + " slot";
            return null;
        }
    }
}

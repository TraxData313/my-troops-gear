using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Writes one set's override onto <paramref name="equipment"/>. Overridden items come without a
        /// modifier (plain quality). An id that no longer resolves leaves that slot as it was.</summary>
        public static void Apply(SetOverride? over, Equipment equipment)
        {
            if (over == null) return;
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

        /// <summary>The troop's vanilla battle sets in game order - the set indices the store uses. Read only.</summary>
        public static List<Equipment> BattleSets(CharacterObject troop)
        {
            try
            {
                return troop.BattleEquipments?.Where(e => e != null).ToList() ?? new List<Equipment>();
            }
            catch (Exception)
            {
                return new List<Equipment>();
            }
        }

        public static int SetCount(CharacterObject troop) => BattleSets(troop).Count;

        /// <summary>A clone of vanilla set <paramref name="set"/> with that set's edits on top (UI previews), or null when
        /// the troop has no such set. The troop's own sets are never written.</summary>
        public static Equipment? EditedSet(CharacterObject troop, TroopOverride? over, int set)
        {
            var sets = BattleSets(troop);
            if (set < 0 || set >= sets.Count) return null;
            var clone = sets[set].Clone();
            Apply(over?.GetSet(set), clone);
            return clone;
        }

        /// <summary>
        /// What the troop's agent spawns with, or null to leave vanilla alone. Vanilla mixes the troop's sets slot by slot
        /// (see <see cref="SpawnSet"/>); we pick ONE set with the agent's seed exactly as vanilla draws its first set
        /// number. If that set has no edits we return null and the soldier rolls vanilla's own mix; otherwise he wears
        /// that set whole: its vanilla items with vanilla's random modifiers, the set's edits on top.
        /// </summary>
        public static Equipment? SpawnEquipment(CharacterObject troop, TroopOverride over, int seed)
        {
            var sets = BattleSets(troop);
            int k = SpawnSet.Pick(seed, sets.Count, n => MBRandom.RandomInt(n));
            var setOver = k < 0 ? null : over.GetSet(k);
            if (setOver == null || setOver.IsEmpty) return null;

            bool modifiers = !Game.Current.GameType.IsCoreOnlyGameMode;
            var equipment = new Equipment(Equipment.EquipmentType.Battle);
            var source = sets[k];
            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var element = source[(EquipmentIndex)i];
                if (modifiers && !setOver.IsOverridden(i))
                {
                    var modifier = element.Item?.ItemComponent?.ItemModifierGroup?.GetRandomItemModifierLootScoreBased();
                    if (modifier != null)
                        element.SetModifier(modifier);
                }
                equipment[(EquipmentIndex)i] = element;
            }
            Apply(setOver, equipment);
            return equipment;
        }

        /// <summary>The item id vanilla set <paramref name="set"/> has in <paramref name="slot"/>, or null for an empty
        /// slot or a missing set.</summary>
        public static string? VanillaItemId(CharacterObject troop, int set, int slot)
        {
            try
            {
                var sets = BattleSets(troop);
                return set >= 0 && set < sets.Count ? sets[set][(EquipmentIndex)slot].Item?.StringId : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A (set, slot) -> vanilla item id lookup over a snapshot of the troop's sets (cheap to call in loops).</summary>
        public static Func<int, int, string?> VanillaOf(CharacterObject troop)
        {
            var sets = BattleSets(troop);
            return (set, slot) =>
            {
                try
                {
                    return set >= 0 && set < sets.Count ? sets[set][(EquipmentIndex)slot].Item?.StringId : null;
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }

        /// <summary>Some set of the troop has a real edit.</summary>
        public static bool HasEdits(CharacterObject troop, TroopOverride? over) =>
            over != null && GearEditing.HasEdits(over, VanillaOf(troop));
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

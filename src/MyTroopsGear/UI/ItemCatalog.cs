using System;
using System.Collections.Generic;
using System.Linq;
using MyTroopsGear.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace MyTroopsGear.UI
{
    /// <summary>
    /// The picker's item lists, built from the game's ItemObjects once per campaign and slot kind (all four weapon slots
    /// share one list). Read-only: nothing here changes an item or a troop.
    /// </summary>
    internal static class ItemCatalog
    {
        private static Campaign? _campaign;
        private static readonly Dictionary<string, List<ItemCandidate>> ByKind = new Dictionary<string, List<ItemCandidate>>();
        private static readonly Dictionary<string, ItemObject> Items = new Dictionary<string, ItemObject>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> CultureNames = new Dictionary<string, string>(StringComparer.Ordinal);
        private static HashSet<string>? _troopItems;

        public static ItemObject? Item(string id) => Items.TryGetValue(id, out var item) ? item : null;

        public static string CultureName(string? id) =>
            id == null ? "Neutral" : CultureNames.TryGetValue(id, out var n) ? n : id;

        /// <summary>Every item that can go into <paramref name="slot"/> (unfiltered; Core's filters do the rest).</summary>
        public static List<ItemCandidate> For(int slot)
        {
            if (_campaign != Campaign.Current)
            {
                ByKind.Clear();
                Items.Clear();
                CultureNames.Clear();
                _troopItems = null;
                _campaign = Campaign.Current;
            }
            var kind = GearSlots.IsWeapon(slot) ? "weapon" : GearSlots.Name(slot);
            if (ByKind.TryGetValue(kind, out var list))
                return list;
            list = Build(slot);
            ByKind[kind] = list;
            return list;
        }

        private static List<ItemCandidate> Build(int slot)
        {
            var result = new List<ItemCandidate>();
            var all = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
            if (all == null)
                return result;
            var troopItems = TroopItems();
            var index = (EquipmentIndex)slot;
            foreach (var item in all)
            {
                try
                {
                    if (item == null || string.IsNullOrEmpty(item.StringId) || item.ItemType == ItemObject.ItemTypeEnum.Invalid)
                        continue;
                    if (!Equipment.IsItemFitsToSlot(index, item))
                        continue;
                    if (slot == GearSlots.Horse && item.HorseComponent?.IsMount != true)
                        continue; // pack animals and livestock
                    var culture = item.Culture;
                    if (culture != null && !CultureNames.ContainsKey(culture.StringId))
                        CultureNames[culture.StringId] = culture.Name?.ToString() ?? culture.StringId;
                    Items[item.StringId] = item;
                    result.Add(new ItemCandidate(item.StringId, item.Name?.ToString() ?? item.StringId,
                        ItemCandidates.TierFromGame((int)item.Tier), SubType(item, slot), culture?.StringId)
                    {
                        IsCraftedByPlayer = item.IsCraftedByPlayer,
                        NotMerchandise = item.NotMerchandise,
                        UsedByTroops = troopItems.Contains(item.StringId),
                        NotUsableByFemale = (item.ItemFlags & ItemFlags.NotUsableByFemale) != 0,
                        NotUsableByMale = (item.ItemFlags & ItemFlags.NotUsableByMale) != 0,
                    });
                }
                catch (Exception e)
                {
                    ModLog.ErrorOnce("catalog item", e);
                }
            }
            ModLog.Info("editor", "picker list for " + GearSlots.Name(slot) + ": " + result.Count + " item(s)");
            return result;
        }

        /// <summary>Items some non-hero troop wears in a vanilla battle set: real troop gear, even when NotMerchandise.</summary>
        private static HashSet<string> TroopItems()
        {
            if (_troopItems != null)
                return _troopItems;
            var set = new HashSet<string>(StringComparer.Ordinal);
            var troops = MBObjectManager.Instance?.GetObjectTypeList<CharacterObject>();
            if (troops != null)
            {
                foreach (var troop in troops)
                {
                    if (troop == null || troop.IsHero)
                        continue;
                    try
                    {
                        foreach (var eq in troop.BattleEquipments)
                        {
                            for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                            {
                                var id = eq[i].Item?.StringId;
                                if (id != null)
                                    set.Add(id);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ModLog.ErrorOnce("catalog troop items", e);
                    }
                }
            }
            _troopItems = set;
            return set;
        }

        private static string SubType(ItemObject item, int slot)
        {
            if (GearSlots.IsWeapon(slot))
                return item.ItemType.ToString();
            if (slot == GearSlots.Horse)
                return item.ItemCategory?.StringId ?? "horse";
            return item.ArmorComponent?.MaterialType.ToString() ?? "None";
        }

        /// <summary>A sub-type key as the filter shows it.</summary>
        public static string SubTypeLabel(string key)
        {
            switch (key)
            {
                case "OneHandedWeapon": return "One-handed";
                case "TwoHandedWeapon": return "Two-handed";
                case "Polearm": return "Polearm";
                case "Arrows": return "Arrows";
                case "Bolts": return "Bolts";
                case "Shield": return "Shield";
                case "Bow": return "Bow";
                case "Crossbow": return "Crossbow";
                case "Thrown": return "Throwing";
                case "Sling": return "Sling";
                case "SlingStones": return "Sling stones";
                case "Pistol": return "Pistol";
                case "Musket": return "Musket";
                case "Bullets": return "Bullets";
                case "None": return "Other";
                default:
                    // horse categories ("war_horse") and armour materials ("Chainmail")
                    var text = key.Replace('_', ' ');
                    return text.Length > 0 ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// What the item picker knows about one game item, flattened so the filtering can be tested without the game.
    /// The module builds these from ItemObjects once per campaign.
    /// </summary>
    public sealed class ItemCandidate
    {
        public ItemCandidate(string id, string name, int tier, string subType, string? cultureId = null)
        {
            Id = id;
            Name = name;
            Tier = tier;
            SubType = subType;
            CultureId = string.IsNullOrEmpty(cultureId) ? null : cultureId;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>1-6 as players see it (the game's ItemTiers.Tier1 = 1).</summary>
        public int Tier { get; }

        /// <summary>The picker's sub-type filter key: the weapon's item type, the armour's material, the horse's category.</summary>
        public string SubType { get; }

        /// <summary>Null = no culture (neutral item).</summary>
        public string? CultureId { get; }

        public bool IsCraftedByPlayer { get; set; }

        /// <summary>The game's NotMerchandise: unique, quest or lord-only items, and some junk.</summary>
        public bool NotMerchandise { get; set; }

        /// <summary>Some non-hero troop has it in a vanilla battle set (so it is a real troop item even if not sold).</summary>
        public bool UsedByTroops { get; set; }

        public bool NotUsableByFemale { get; set; }

        public bool NotUsableByMale { get; set; }

        public override string ToString() => Id + " (T" + Tier + ", " + SubType + ", " + (CultureId ?? "neutral") + ")";
    }

    /// <summary>How the culture filter reads.</summary>
    public enum CultureMode
    {
        /// <summary>Every culture and neutral.</summary>
        Any,
        /// <summary>The given culture plus neutral items (the default, with the troop's culture).</summary>
        CultureAndNeutral,
        /// <summary>Only the given culture.</summary>
        CultureOnly,
        /// <summary>Only items without a culture.</summary>
        NeutralOnly,
    }

    /// <summary>The picker's current filters.</summary>
    public sealed class CandidateQuery
    {
        /// <summary>Highest tier shown (1-6); null = any tier.</summary>
        public int? MaxTier { get; set; }

        public CultureMode Culture { get; set; } = CultureMode.Any;

        /// <summary>The culture of <see cref="CultureMode.CultureAndNeutral"/> / <see cref="CultureMode.CultureOnly"/>.</summary>
        public string? CultureId { get; set; }

        /// <summary>Null or empty = every sub-type.</summary>
        public string? SubType { get; set; }

        /// <summary>Case-insensitive substring of the name (or the id); blank = no search.</summary>
        public string? Search { get; set; }

        /// <summary>The troop is female: items not usable by women are left out (and vice versa).</summary>
        public bool IsFemale { get; set; }
    }

    public static class ItemCandidates
    {
        public const int MinTier = 1;
        public const int MaxTier = 6;

        /// <summary>The picker's default tier cap: one above the troop's tier, within 1-6.</summary>
        public static int DefaultMaxTier(int troopTier) => Math.Max(MinTier, Math.Min(MaxTier, troopTier + 1));

        /// <summary>The game's ItemTiers enum value (Tier1 = 0) as players count it (1-6), clamped.</summary>
        public static int TierFromGame(int itemTiersValue) => Math.Max(MinTier, Math.Min(MaxTier, itemTiersValue + 1));

        /// <summary>
        /// Never offered, whatever the filters: player-crafted items (they belong to one save), NotMerchandise items
        /// that no vanilla troop wears (quest, unique and junk items), and items the troop's gender can't use.
        /// </summary>
        public static bool IsAllowed(ItemCandidate c, bool isFemale)
        {
            if (c.IsCraftedByPlayer) return false;
            if (c.NotMerchandise && !c.UsedByTroops) return false;
            if (isFemale ? c.NotUsableByFemale : c.NotUsableByMale) return false;
            return true;
        }

        /// <summary>The candidates that pass <see cref="IsAllowed"/> and every filter of <paramref name="q"/>,
        /// sorted by tier, then name, then id.</summary>
        public static List<ItemCandidate> Filter(IEnumerable<ItemCandidate> all, CandidateQuery q)
        {
            var search = string.IsNullOrWhiteSpace(q.Search) ? null : q.Search!.Trim();
            return all
                .Where(c => IsAllowed(c, q.IsFemale))
                .Where(c => q.MaxTier == null || c.Tier <= q.MaxTier.Value)
                .Where(c => MatchesCulture(c, q))
                .Where(c => string.IsNullOrEmpty(q.SubType) || string.Equals(c.SubType, q.SubType, StringComparison.Ordinal))
                .Where(c => search == null
                            || c.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                            || c.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(c => c.Tier)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Id, StringComparer.Ordinal)
                .ToList();
        }

        public static bool MatchesCulture(ItemCandidate c, CandidateQuery q)
        {
            switch (q.Culture)
            {
                case CultureMode.CultureAndNeutral:
                    return c.CultureId == null || string.Equals(c.CultureId, q.CultureId, StringComparison.Ordinal);
                case CultureMode.CultureOnly:
                    return c.CultureId != null && string.Equals(c.CultureId, q.CultureId, StringComparison.Ordinal);
                case CultureMode.NeutralOnly:
                    return c.CultureId == null;
                default:
                    return true;
            }
        }

        /// <summary>The sub-types present among the allowed candidates, for the sub-type filter, sorted.</summary>
        public static List<string> SubTypes(IEnumerable<ItemCandidate> all, bool isFemale) =>
            all.Where(c => IsAllowed(c, isFemale)).Select(c => c.SubType).Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

        /// <summary>The cultures present among the allowed candidates (neutral not included), sorted.</summary>
        public static List<string> Cultures(IEnumerable<ItemCandidate> all, bool isFemale) =>
            all.Where(c => IsAllowed(c, isFemale) && c.CultureId != null).Select(c => c.CultureId!)
                .Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
    }
}

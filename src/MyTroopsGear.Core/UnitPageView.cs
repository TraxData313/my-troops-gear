using System;
using System.Collections.Generic;

namespace MyTroopsGear.Core
{
    /// <summary>What the Encyclopedia unit page must do to its equipment sets.</summary>
    public enum UnitPageAction
    {
        None,
        ShowVanilla,
        ShowMine,
    }

    /// <summary>
    /// The pure rules of the unit page's "Vanilla | My version" toggle. The choice is remembered for the game session
    /// (browsing several of my units stays in My version), but a page only shows My version when its troop has an
    /// override; every other page stays vanilla.
    /// </summary>
    public static class UnitPageView
    {
        /// <param name="wantMine">The remembered choice.</param>
        /// <param name="canShowMine">This page's troop is a non-hero with an override in this campaign.</param>
        /// <param name="shownMine">The page shows My version right now.</param>
        /// <param name="force">Rebuild even when the mode does not change (the edits changed).</param>
        public static UnitPageAction Decide(bool wantMine, bool canShowMine, bool shownMine, bool force)
        {
            var mine = wantMine && canShowMine;
            if (mine) return shownMine && !force ? UnitPageAction.None : UnitPageAction.ShowMine;
            return shownMine ? UnitPageAction.ShowVanilla : UnitPageAction.None;
        }

        /// <summary>The items in order, each kept only if no earlier kept item equals it - the way the vanilla page
        /// drops duplicate equipment sets.</summary>
        public static List<T> DistinctInOrder<T>(IEnumerable<T> items, Func<T, T, bool> equal)
        {
            var kept = new List<T>();
            foreach (var item in items)
            {
                var seen = false;
                foreach (var k in kept)
                {
                    if (equal(k, item)) { seen = true; break; }
                }
                if (!seen) kept.Add(item);
            }
            return kept;
        }

        /// <summary>The set index to keep after the list is rebuilt with <paramref name="count"/> sets.</summary>
        public static int KeepIndex(int current, int count) =>
            count <= 0 ? -1 : Math.Min(Math.Max(current, 0), count - 1);
    }
}

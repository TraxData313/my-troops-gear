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
    /// Owns the campaign's <see cref="OverrideStore"/> and saves it. The save holds ONE list of plain strings
    /// (<c>troopId|set|slot|itemId</c>) under <see cref="SaveKey"/> - no SaveableTypeDefiner, no custom types - so
    /// the mod can be added to or removed from a running campaign: without the mod the game simply ignores the
    /// orphaned entry, and a save without it starts with an empty store.
    /// </summary>
    internal sealed class GearBehavior : CampaignBehaviorBase
    {
        /// <summary>Per-set lines <c>troopId|set|slot|itemId</c> (since per-set editing).</summary>
        public const string SaveKey = "MyTroopsGear_overrides_v2";

        /// <summary>Older builds: <c>troopId|slot|itemId</c>, one edit for every set. Read on load only; the store keeps
        /// those lines as all-sets edits until <see cref="OnSessionLaunched"/> expands them onto each set.</summary>
        public const string OldSaveKey = "MyTroopsGear_overrides_v1";

        public static GearBehavior? Instance { get; private set; }

        private List<string> _lines = new List<string>();

        public GearBehavior()
        {
            Instance = this;
        }

        /// <summary>The overrides. Replaced (not refilled) when a save loads, so look it up each time.</summary>
        public OverrideStore Store { get; private set; } = new OverrideStore();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (!dataStore.IsLoading)
                _lines = Store.ToLines();
            dataStore.SyncData(SaveKey, ref _lines);
            if (dataStore.IsLoading)
            {
                var old = new List<string>();
                dataStore.SyncData(OldSaveKey, ref old); // absent in new saves: stays empty
                var all = (old ?? new List<string>()).Concat(_lines ?? new List<string>()).ToList();
                Store = OverrideStore.FromLines(all, out var skipped);
                ModLog.Info("load", "read " + all.Count + " saved line(s) (" + (old?.Count ?? 0) + " old all-sets): "
                    + Store.Count + " troop(s) per set, " + Store.PendingAllSetsCount + " troop(s) to expand"
                    + (skipped > 0 ? ", skipped " + skipped + " malformed line(s)" : ""));
                _lines = new List<string>();
            }
        }

        // Checked once the game's objects are all there: drop troops and items that no longer exist
        // (an item or troop mod was removed since the save).
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                var report = Store.Prune(TroopExists, ItemUsable, SetCountOf);
                foreach (var line in report)
                    ModLog.Info("load", line);
                // Stored slots that equal their set's vanilla item change nothing (older builds kept unchanged
                // blocks; expanded all-sets edits often match some set's own item): drop them per set.
                foreach (var id in Store.TroopIds.ToList())
                {
                    var troop = MBObjectManager.Instance.GetObject<CharacterObject>(id);
                    if (troop == null) continue;
                    var vanillaOf = GearApplier.VanillaOf(troop);
                    foreach (var set in Store.Get(id)?.SetIndices.ToList() ?? new List<int>())
                    {
                        if (GearEditing.DropUnchanged(Store, id, set, s => vanillaOf(set, s)))
                            ModLog.Info("load", id + " set " + (set + 1) + ": dropped slots equal to vanilla");
                    }
                }
                ModLog.Info("session", "campaign ready, " + Store.Count + " troop override(s)");
            }
            catch (Exception e)
            {
                ModLog.Error("load", "checking saved overrides against the game failed, keeping them as read", e);
            }
        }

        internal static bool TroopExists(string id)
        {
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(id);
            return troop != null && !troop.IsHero;
        }

        internal static int SetCountOf(string id)
        {
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(id);
            return troop == null ? 0 : GearApplier.SetCount(troop);
        }

        internal static bool ItemUsable(string id)
        {
            var item = MBObjectManager.Instance.GetObject<ItemObject>(id);
            return item != null && !item.IsCraftedByPlayer;
        }
    }
}

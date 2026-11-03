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
    /// (<c>troopId|slot|itemId</c>) under <see cref="SaveKey"/> - no SaveableTypeDefiner, no custom types - so
    /// the mod can be added to or removed from a running campaign: without the mod the game simply ignores the
    /// orphaned entry, and a save without it starts with an empty store.
    /// </summary>
    internal sealed class GearBehavior : CampaignBehaviorBase
    {
        public const string SaveKey = "MyTroopsGear_overrides_v1";

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
                Store = OverrideStore.FromLines(_lines, out var skipped);
                ModLog.Info("load", "read " + (_lines?.Count ?? 0) + " saved line(s) for " + Store.Count + " troop(s)"
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
                var report = Store.Prune(TroopExists, ItemUsable);
                foreach (var line in report)
                    ModLog.Info("load", line);
                // A weapon block / mount pair left on all-vanilla items (older builds kept them) is dropped, so
                // the troop rolls its vanilla sets again there.
                foreach (var id in Store.TroopIds.ToList())
                {
                    var troop = MBObjectManager.Instance.GetObject<CharacterObject>(id);
                    if (troop != null && GearEditing.DropVanillaBlocks(Store, id, s => GearApplier.VanillaItemId(troop, s)))
                        ModLog.Info("load", id + ": dropped an unchanged weapon/mount block");
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

        internal static bool ItemUsable(string id)
        {
            var item = MBObjectManager.Instance.GetObject<ItemObject>(id);
            return item != null && !item.IsCraftedByPlayer;
        }
    }
}

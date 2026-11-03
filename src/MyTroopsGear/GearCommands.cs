using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MyTroopsGear.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace MyTroopsGear
{
    /// <summary>
    /// Debug console commands (open the console with Ctrl + ~ in a campaign). They are the test bench for the
    /// override store until the editor window exists, and stay useful afterwards. Slots are 0-3 (weapons),
    /// 5 head, 6 body, 7 leg, 8 gloves, 9 cape, 10 horse, 11 harness - or those names.
    /// </summary>
    public static class GearCommands
    {
        private const string SetUsage = "Usage: mytroopsgear.set <troopId> <slot> <itemId|empty|vanilla>\n"
            + "slot: 0-3 or weapon0-weapon3, 5 head, 6 body, 7 leg, 8 gloves, 9 cape, 10 horse, 11 harness.\n"
            + "A first weapon edit takes over all 4 weapon slots (the others start from the troop's vanilla set); "
            + "horse + harness likewise. 'vanilla' reverts the slot (its whole block for weapons / mount).";

        [CommandLineFunctionality.CommandLineArgumentFunction("set", "mytroopsgear")]
        public static string Set(List<string> args)
        {
            try
            {
                if (!TryGetStore(out var store, out var error)) return error;
                if (args == null || args.Count != 3) return SetUsage;

                var troop = MBObjectManager.Instance.GetObject<CharacterObject>(args[0]);
                if (troop == null) return "No troop with id '" + args[0] + "'.\n" + SetUsage;
                if (troop.IsHero) return "'" + args[0] + "' is a hero; only regular troops can be edited.";
                if (!GearSlots.TryParse(args[1], out var slot)) return "Bad slot '" + args[1] + "'.\n" + SetUsage;

                var itemArg = args[2].Trim();
                if (string.Equals(itemArg, "vanilla", StringComparison.OrdinalIgnoreCase))
                {
                    store.Revert(troop.StringId, slot);
                    ModLog.Info("console", "revert " + troop.StringId + " " + GearSlots.Name(slot));
                    return "Reverted " + Describe(troop) + " " + GearSlots.Name(slot) + " to vanilla.\n" + ListOne(store, troop.StringId);
                }

                string itemId;
                if (string.Equals(itemArg, GearSlots.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    itemId = GearSlots.Empty;
                }
                else
                {
                    var item = MBObjectManager.Instance.GetObject<TaleWorlds.Core.ItemObject>(itemArg);
                    if (item == null) return "No item with id '" + itemArg + "'.";
                    var why = GearApplier.WhyNotFits(item, slot);
                    if (why != null) return "Can not use " + itemArg + ": " + why + ".";
                    itemId = item.StringId;
                }

                store.Set(troop.StringId, slot, itemId, s => GearApplier.VanillaItemId(troop, s));
                ModLog.Info("console", "set " + troop.StringId + " " + GearSlots.Name(slot) + " = " + itemId);
                return "Set.\n" + ListOne(store, troop.StringId);
            }
            catch (Exception e)
            {
                ModLog.Error("console", "mytroopsgear.set failed", e);
                return "Failed: " + e.Message;
            }
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("clear", "mytroopsgear")]
        public static string Clear(List<string> args)
        {
            try
            {
                if (!TryGetStore(out var store, out var error)) return error;
                if (args == null || args.Count == 0)
                {
                    int n = store.Count;
                    store.ClearAll();
                    ModLog.Info("console", "clear all (" + n + " troop(s))");
                    return "Cleared every override (" + n + " troop(s)). All troops are vanilla again.";
                }
                if (args.Count != 1) return "Usage: mytroopsgear.clear [troopId]   (no id = clear all)";
                if (!store.Clear(args[0])) return "'" + args[0] + "' has no override.";
                ModLog.Info("console", "clear " + args[0]);
                return "Cleared " + args[0] + ", it is vanilla again.";
            }
            catch (Exception e)
            {
                ModLog.Error("console", "mytroopsgear.clear failed", e);
                return "Failed: " + e.Message;
            }
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("list", "mytroopsgear")]
        public static string List(List<string> args)
        {
            try
            {
                if (!TryGetStore(out var store, out var error)) return error;
                if (store.Count == 0) return "No overrides: every troop is vanilla.";
                var text = new StringBuilder();
                text.Append(store.Count).Append(" troop override(s):\n");
                foreach (var id in store.TroopIds.ToList())
                    text.Append(ListOne(store, id));
                return text.ToString();
            }
            catch (Exception e)
            {
                ModLog.Error("console", "mytroopsgear.list failed", e);
                return "Failed: " + e.Message;
            }
        }

        private static bool TryGetStore(out OverrideStore store, out string error)
        {
            store = null!;
            error = "";
            var behavior = GearBehavior.Instance;
            if (Campaign.Current == null || behavior == null)
            {
                error = "Start or load a campaign first.";
                return false;
            }
            store = behavior.Store;
            return true;
        }

        private static string Describe(CharacterObject troop) => troop.Name + " (" + troop.StringId + ")";

        private static string ListOne(OverrideStore store, string troopId)
        {
            var over = store.Get(troopId);
            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
            var text = new StringBuilder();
            text.Append(troop != null ? Describe(troop) : troopId).Append(':');
            if (over == null || over.IsEmpty) return text.Append(" vanilla\n").ToString();
            text.Append('\n');
            foreach (var slot in over.Slots)
            {
                var name = slot.Value == GearSlots.Empty ? "(empty)" : GearApplier.FindItem(slot.Value)?.Name?.ToString() ?? "?";
                text.Append("  ").Append(slot.Key).Append(' ').Append(GearSlots.Name(slot.Key)).Append(": ")
                    .Append(slot.Value).Append(slot.Value == GearSlots.Empty ? "" : " - " + name).Append('\n');
            }
            return text.ToString();
        }
    }
}

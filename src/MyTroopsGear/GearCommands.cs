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
        private const string SetUsage = "Usage: mytroopsgear.set <troopId> <set|all> <slot> <itemId|empty|vanilla>\n"
            + "set: the vanilla battle set as the editor counts it (1 = first), or 'all' for every set of the troop.\n"
            + "slot: 0-3 or weapon0-weapon3, 5 head, 6 body, 7 leg, 8 gloves, 9 cape, 10 horse, 11 harness.\n"
            + "A first weapon edit of a set takes over its 4 weapon slots (the others start from that set's vanilla items); "
            + "horse + harness likewise. 'vanilla' resets the slot in that set (a weapon/mount slot goes back to the set's "
            + "own item; once the whole block is back, the set is vanilla there).";

        private const string ClearUsage = "Usage: mytroopsgear.clear [troopId [set]]   (no id = clear every troop; "
            + "no set = every set of that troop)";

        [CommandLineFunctionality.CommandLineArgumentFunction("set", "mytroopsgear")]
        public static string Set(List<string> args)
        {
            try
            {
                if (!TryGetStore(out var store, out var error)) return error;
                if (args == null || args.Count != 4) return SetUsage;

                var troop = MBObjectManager.Instance.GetObject<CharacterObject>(args[0]);
                if (troop == null) return "No troop with id '" + args[0] + "'.\n" + SetUsage;
                if (troop.IsHero) return "'" + args[0] + "' is a hero; only regular troops can be edited.";
                int count = GearApplier.SetCount(troop);
                if (count == 0) return Describe(troop) + " has no battle set.";
                if (!TryParseSets(args[1], count, out var sets))
                    return "Bad set '" + args[1] + "': " + Describe(troop) + " has sets 1-" + count + " (or 'all').\n" + SetUsage;
                if (!GearSlots.TryParse(args[2], out var slot)) return "Bad slot '" + args[2] + "'.\n" + SetUsage;

                var vanillaOf = GearApplier.VanillaOf(troop);
                var itemArg = args[3].Trim();
                if (string.Equals(itemArg, "vanilla", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var k in sets)
                        GearEditing.RevertSlot(store, troop.StringId, k, slot, s => vanillaOf(k, s));
                    ModLog.Info("console", "reset " + troop.StringId + " set(s) " + SetsText(sets) + " " + GearSlots.Name(slot));
                    return "Reset " + Describe(troop) + " " + GearSlots.Name(slot) + " in set(s) " + SetsText(sets) + ".\n"
                           + ListOne(store, troop.StringId);
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

                foreach (var k in sets)
                    GearEditing.SetSlot(store, troop.StringId, k, slot, itemId, s => vanillaOf(k, s));
                ModLog.Info("console", "set " + troop.StringId + " set(s) " + SetsText(sets) + " " + GearSlots.Name(slot) + " = " + itemId);
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
                if (args.Count == 2)
                {
                    if (!int.TryParse(args[1], out var number) || number < 1) return "Bad set '" + args[1] + "'.\n" + ClearUsage;
                    if (!store.ClearSet(args[0], number - 1)) return "'" + args[0] + "' has no edits in set " + number + ".";
                    ModLog.Info("console", "clear " + args[0] + " set " + number);
                    return "Cleared set " + number + " of " + args[0] + ".\n" + ListOne(store, args[0]);
                }
                if (args.Count != 1) return ClearUsage;
                if (!store.Clear(args[0])) return "'" + args[0] + "' has no override.";
                ModLog.Info("console", "clear " + args[0]);
                return "Cleared " + args[0] + ", every set is vanilla again.";
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
                if (args != null && args.Count == 1) return ListOne(store, args[0]);
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

        /// <summary>"all" = every set; else a 1-based set number within the troop's <paramref name="count"/> sets.</summary>
        private static bool TryParseSets(string text, int count, out List<int> sets)
        {
            sets = new List<int>();
            if (string.Equals(text?.Trim(), "all", StringComparison.OrdinalIgnoreCase))
            {
                sets.AddRange(Enumerable.Range(0, count));
                return true;
            }
            if (!int.TryParse(text?.Trim(), out var number) || number < 1 || number > count) return false;
            sets.Add(number - 1);
            return true;
        }

        private static string SetsText(List<int> sets) => string.Join(",", sets.Select(k => (k + 1).ToString()));
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
            if (troop != null) text.Append(' ').Append(GearApplier.SetCount(troop)).Append(" set(s)");
            text.Append('\n');
            foreach (var set in over.Sets)
            {
                text.Append("  set ").Append(set.Key + 1).Append(":\n");
                foreach (var slot in set.Value.Slots)
                {
                    var name = slot.Value == GearSlots.Empty ? "(empty)" : GearApplier.FindItem(slot.Value)?.Name?.ToString() ?? "?";
                    text.Append("    ").Append(slot.Key).Append(' ').Append(GearSlots.Name(slot.Key)).Append(": ")
                        .Append(slot.Value).Append(slot.Value == GearSlots.Empty ? "" : " - " + name).Append('\n');
                }
            }
            return text.ToString();
        }
    }
}

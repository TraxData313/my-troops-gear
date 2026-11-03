using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;

namespace MyTroopsGear.UI
{
    /// <summary>
    /// The Encyclopedia goes back a page on Backspace by reading the GLOBAL keyboard (Input.IsKeyPressed, not its
    /// layer's input) in its own tick, so Backspace in our editor - in the search box above all - would page the
    /// Encyclopedia back under the open editor. This prefix skips EncyclopediaNavigatorVM.ExecuteBack and
    /// ExecuteForward while the editor is open; nothing changes otherwise.
    /// </summary>
    internal static class EncyclopediaBackGuard
    {
        public static void Apply(Harmony harmony)
        {
            var prefix = new HarmonyMethod(typeof(EncyclopediaBackGuard), nameof(Prefix));
            foreach (var name in new[] { nameof(EncyclopediaNavigatorVM.ExecuteBack), nameof(EncyclopediaNavigatorVM.ExecuteForward) })
            {
                var target = AccessTools.Method(typeof(EncyclopediaNavigatorVM), name)
                             ?? throw new MissingMethodException("EncyclopediaNavigatorVM." + name + " not found");
                harmony.Patch(target, prefix: prefix);
            }
        }

        private static bool Prefix() => !GearEditorWindow.IsOpen;
    }
}

using System;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MyTroopsGear
{
    public class SubModule : MBSubModuleBase
    {
        internal const string HarmonyId = "trax.bannerlord.mytroopsgear";

        private static bool _patched;
        private static UIExtender? _uiExtender;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                // The Encyclopedia unit page's "Edit gear" button (UI\EncyclopediaUnitPageButtons.cs).
                _uiExtender = UIExtender.Create("MyTroopsGear");
                _uiExtender.Register(typeof(SubModule).Assembly);
                _uiExtender.Enable();
            }
            catch (Exception e)
            {
                ModLog.Error("load", "UIExtenderEx setup failed", e);
            }
        }

        // Patched on the first campaign start rather than in OnSubModuleLoad, so nothing the patched methods
        // touch is JIT-compiled before a campaign exists.
        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (!(game.GameType is Campaign) || !(gameStarterObject is CampaignGameStarter starter))
                return;
            starter.AddBehavior(new GearBehavior());
            if (_patched)
                return;
            _patched = true;
            if (!GearApplier.SlotsMatchGame)
            {
                Warn("the game's equipment slots changed, edited gear is OFF until the mod is updated");
                return;
            }
            try
            {
                UI.EncyclopediaBackGuard.Apply(new Harmony(HarmonyId));
            }
            catch (Exception e)
            {
                ModLog.Error("load", "patching the Encyclopedia's Back/Forward failed (Backspace in the editor may page it back)", e);
            }
            try
            {
                UI.PartyScreenPatch.Apply(new Harmony(HarmonyId));
                ModLog.Info("load", "party screen patch applied");
            }
            catch (Exception e)
            {
                ModLog.Error("load", "patching the party screen troop images failed (cards stay vanilla)", e);
            }
            try
            {
                UI.PartyScreenPatch.ApplySelected(new Harmony(HarmonyId));
                ModLog.Info("load", "party screen selected-troop patch applied");
            }
            catch (Exception e)
            {
                ModLog.Error("load", "patching the party screen's big troop model failed (it stays vanilla)", e);
            }
            try
            {
                SpawnPatch.Apply(new Harmony(HarmonyId));
                ModLog.Info("load", "spawn patch applied");
            }
            catch (Exception e)
            {
                ModLog.Error("load", "patching Mission.SpawnAgent failed", e);
                Warn("patching troop spawns failed, troops fight in vanilla gear (see the mod log)");
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            UI.GearEditorWindow.Tick(dt); // cheap while the editor is closed
        }

        public override void OnGameEnd(Game game)
        {
            UI.GearEditorWindow.Close();
            UI.EncyclopediaUnitPageMixin.ResetSession();
            base.OnGameEnd(game);
        }

        internal static void Warn(string message)
        {
            ModLog.Info("warn", message);
            InformationManager.DisplayMessage(new InformationMessage("My Troops' Gear: " + message, Colors.Yellow));
        }
    }
}

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace MyTroopsGear.UI
{
    /// <summary>
    /// The gear editor's host: one Gauntlet layer with the movie <c>module\GUI\Prefabs\MyTroopsGearEditor.xml</c>,
    /// opened from the Encyclopedia unit page. Layer order 320 sits ABOVE the Encyclopedia (310): keyboard input only
    /// reaches the focused layer (ScreenManager.EarlyUpdate), so while we are open the Encyclopedia never sees Escape,
    /// and our full-screen backdrop takes the clicks. On close the focus goes back to the highest focus layer left -
    /// the Encyclopedia. Everything is best-effort: a failure closes the window, never the game.
    /// </summary>
    internal static class GearEditorWindow
    {
        public const int LayerOrder = 320;
        public const string MovieName = "MyTroopsGearEditor";

        private static GauntletLayer? _layer;
        private static GauntletMovieIdentifier? _movie;
        private static ScreenBase? _host;
        private static GearEditorVM? _vm;
        private static GearBehavior? _behavior;
        private static bool _closeRequested;

        public static bool IsOpen => _layer != null;

        public static void Open(CharacterObject troop)
        {
            if (IsOpen)
                Close();
            if (Campaign.Current == null || GearBehavior.Instance == null)
            {
                SubModule.Warn("the gear editor works only in a loaded campaign");
                return;
            }
            if (troop == null || troop.IsHero)
                return;
            try
            {
                _behavior = GearBehavior.Instance;
                _vm = new GearEditorVM(troop);
                _layer = new GauntletLayer("MyTroopsGearEditor", LayerOrder) { IsFocusLayer = true };
                _movie = _layer.LoadMovie(MovieName, _vm);
                _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
                _layer.InputRestrictions.SetInputRestrictions();
                _host = ScreenManager.TopScreen;
                _host.AddLayer(_layer);
                ScreenManager.TrySetFocus(_layer);
                _closeRequested = false;
                ModLog.Info("editor", "opened for " + troop.StringId);
            }
            catch (Exception e)
            {
                ModLog.Error("editor", "opening the gear editor for " + troop?.StringId, e);
                Close();
                SubModule.Warn("the gear editor could not open (see the mod log)");
            }
        }

        /// <summary>Close on the next tick: used from button commands, so the movie is not released under the click.</summary>
        public static void RequestClose() => _closeRequested = true;

        /// <summary>Movie released before the layer goes, input and focus given back, view model finalized. Safe twice.</summary>
        public static void Close()
        {
            var layer = _layer;
            var movie = _movie;
            var host = _host;
            var vm = _vm;
            _layer = null;
            _movie = null;
            _host = null;
            _vm = null;
            _behavior = null;
            _closeRequested = false;

            EditorColors.HideTooltip();
            if (layer != null)
            {
                try
                {
                    layer.IsFocusLayer = false;
                    if (movie != null)
                        layer.ReleaseMovie(movie);
                    layer.InputRestrictions.ResetInputRestrictions();
                    host?.RemoveLayer(layer);
                    ScreenManager.TryLoseFocus(layer);
                }
                catch (Exception e)
                {
                    ModLog.Error("editor", "closing the layer", e);
                }
            }
            if (vm != null)
            {
                try
                {
                    vm.OnFinalize();
                }
                catch (Exception e)
                {
                    ModLog.Error("editor", "finalizing the view model", e);
                }
                ModLog.Info("editor", "closed");
                EditorClosed?.Invoke(vm.Troop);
            }
        }

        /// <summary>Raised after the window closed (the Encyclopedia page can refresh its look).</summary>
        public static event Action<CharacterObject>? EditorClosed;

        /// <summary>Every application tick (SubModule). Cheap when closed.</summary>
        public static void Tick(float dt)
        {
            if (_layer == null)
                return;
            try
            {
                if (_closeRequested)
                {
                    Close();
                    return;
                }
                // The campaign went away or changed (load, exit to menu), or our screen is no longer on top.
                if (Campaign.Current == null || GearBehavior.Instance != _behavior || ScreenManager.TopScreen != _host)
                {
                    ModLog.Info("editor", "campaign or screen changed - closing");
                    Close();
                    return;
                }
                if (_layer.Input.IsHotKeyReleased("Exit"))
                {
                    // Escape in the search box leaves the box first; the next Escape closes the window.
                    if (_layer.IsFocusedOnInput())
                    {
                        _layer.UIContext?.EventManager?.ClearFocus();
                        return;
                    }
                    Close();
                    return;
                }
                _vm?.Tick(dt);
            }
            catch (Exception e)
            {
                ModLog.Error("editor", "tick", e);
                Close();
            }
        }
    }
}

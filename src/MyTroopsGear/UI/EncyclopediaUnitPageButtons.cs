using System;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using MyTroopsGear.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;

namespace MyTroopsGear.UI
{
    // The bar of our buttons on the Encyclopedia unit page: a row just above the vanilla equipment-set arrows, inside the
    // left (tableau) column (370 wide), inserted as the next sibling of the vanilla BookmarkButton:
    // [Vanilla][My version]  [Edit gear]. The toggle shows only when the troop has an override in this campaign.
    [PrefabExtension("EncyclopediaUnitPage", "descendant::ButtonWidget[@Id='BookmarkButton']")]
    public sealed class EncyclopediaUnitPageBarPrefab : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionText]
        public string Content => @"
<ListPanel Id=""MyTroopsGearBar"" WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren"" HorizontalAlignment=""Center"" VerticalAlignment=""Bottom"" MarginBottom=""104"" StackLayout.LayoutMethod=""HorizontalLeftToRight"" IsVisible=""@IsEditGearVisible"">
  <Children>
    <ListPanel Id=""MyTroopsGearViewToggle"" WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren"" MarginRight=""8"" StackLayout.LayoutMethod=""HorizontalLeftToRight"" IsVisible=""@IsViewToggleVisible"">
      <Children>
        <ButtonWidget Id=""MyTroopsGearShowVanilla"" DoNotPassEventsToChildren=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed"" SuggestedWidth=""92"" SuggestedHeight=""40"" Brush=""ButtonBrush2"" Command.Click=""ExecuteShowVanilla"" UpdateChildrenStates=""true"">
          <Children>
            <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Sprite=""BlankWhiteSquare_9"" Color=""#B8893EFF"" AlphaFactor=""0.35"" IsVisible=""@IsVanillaShown"" />
            <TextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Brush=""Popup.Button.Text"" Brush.FontSize=""16"" Text=""Vanilla"" />
            <HintWidget DataSource=""{ShowVanillaHint}"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Command.HoverBegin=""ExecuteBeginHint"" Command.HoverEnd=""ExecuteEndHint"" IsDisabled=""true"" />
          </Children>
        </ButtonWidget>
        <ButtonWidget Id=""MyTroopsGearShowMine"" DoNotPassEventsToChildren=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed"" SuggestedWidth=""110"" SuggestedHeight=""40"" MarginLeft=""2"" Brush=""ButtonBrush2"" Command.Click=""ExecuteShowMine"" UpdateChildrenStates=""true"">
          <Children>
            <Widget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Sprite=""BlankWhiteSquare_9"" Color=""#B8893EFF"" AlphaFactor=""0.35"" IsVisible=""@IsMyVersionShown"" />
            <TextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Brush=""Popup.Button.Text"" Brush.FontSize=""16"" Text=""My version"" />
            <HintWidget DataSource=""{ShowMineHint}"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Command.HoverBegin=""ExecuteBeginHint"" Command.HoverEnd=""ExecuteEndHint"" IsDisabled=""true"" />
          </Children>
        </ButtonWidget>
      </Children>
    </ListPanel>
    <ButtonWidget Id=""MyTroopsGearEditButton"" DoNotPassEventsToChildren=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed"" SuggestedWidth=""150"" SuggestedHeight=""40"" Brush=""ButtonBrush2"" Command.Click=""ExecuteEditGear"" UpdateChildrenStates=""true"">
      <Children>
        <TextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Brush=""Popup.Button.Text"" Brush.FontSize=""18"" Text=""@EditGearText"" />
        <HintWidget DataSource=""{EditGearHint}"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Command.HoverBegin=""ExecuteBeginHint"" Command.HoverEnd=""ExecuteEndHint"" IsDisabled=""true"" />
      </Children>
    </ButtonWidget>
  </Children>
</ListPanel>";
    }

    // "RefreshValues": the vanilla RefreshValues rebuilds the set selector from the troop's own sets (language change
    // etc.); OnRefresh puts My version back after it.
    [ViewModelMixin("RefreshValues")]
    public sealed class EncyclopediaUnitPageMixin : BaseViewModelMixin<EncyclopediaUnitPageVM>
    {
        private static readonly AccessTools.FieldRef<EncyclopediaUnitPageVM, CharacterObject>? CharacterField = TryField();

        private static readonly AccessTools.FieldRef<SelectorVM<EncyclopediaUnitEquipmentSetSelectorItemVM>, Action<SelectorVM<EncyclopediaUnitEquipmentSetSelectorItemVM>>>?
            OnChangeField = TryOnChangeField();

        /// <summary>The remembered choice for this game session: browsing several of my units stays in My version.
        /// Pages of troops without an override always show vanilla, whatever this says.</summary>
        private static bool _wantMine;

        private readonly CharacterObject? _troop;

        /// <summary>The page's sets are override-applied clones right now.</summary>
        private bool _shownMine;

        public EncyclopediaUnitPageMixin(EncyclopediaUnitPageVM vm) : base(vm)
        {
            try
            {
                _troop = CharacterField?.Invoke(vm);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("encyclopedia troop", e);
            }
            GearEditorWindow.EditorClosed += OnEditorClosed;
            ApplyView(force: false);
        }

        /// <summary>Back to the default (Vanilla) for a new game session.</summary>
        public static void ResetSession() => _wantMine = false;

        public override void OnRefresh()
        {
            // The vanilla rebuild put the troop's own sets back (and, when the index stayed 0, left
            // CurrentSelectedEquipmentSet on our old item): rebuild whichever view is wanted.
            var wasMine = _shownMine;
            _shownMine = false;
            if (wasMine && !(_wantMine && CanShowMine))
            {
                try
                {
                    ShowSets(mine: false);
                }
                catch (Exception e)
                {
                    ModLog.ErrorOnce("unit page view", e);
                }
            }
            ApplyView(force: false);
        }

        public override void OnFinalize()
        {
            GearEditorWindow.EditorClosed -= OnEditorClosed;
            base.OnFinalize();
        }

        [DataSourceProperty]
        public bool IsEditGearVisible => _troop != null && !_troop.IsHero && Campaign.Current != null && GearBehavior.Instance != null;

        [DataSourceProperty]
        public bool IsViewToggleVisible => CanShowMine;

        [DataSourceProperty]
        public bool IsMyVersionShown => _shownMine;

        [DataSourceProperty]
        public bool IsVanillaShown => !_shownMine;

        [DataSourceProperty]
        public string EditGearText => HasOverride ? "Edit gear *" : "Edit gear";

        [DataSourceProperty]
        public HintViewModel EditGearHint { get; } = EditorColors.Hint(
            "Change this troop's battle gear, slot by slot. Free and instant; only troops of your own parties wear it. "
            + "* = you have edited this troop.");

        [DataSourceProperty]
        public HintViewModel ShowVanillaHint { get; } = EditorColors.Hint(
            "Show the game's own gear for this troop - what everyone else's parties wear.");

        [DataSourceProperty]
        public HintViewModel ShowMineHint { get; } = EditorColors.Hint(
            "Show this troop as your parties field it: each vanilla set with that set's edits on top. "
            + "Remembered while you browse; troops you have not edited always show vanilla.");

        /// <summary>Some set of the troop has a really edited slot (a block mate stored unchanged does not count).</summary>
        private bool HasOverride
        {
            get
            {
                var troop = _troop;
                if (troop == null) return false;
                return GearApplier.HasEdits(troop, GearBehavior.Instance?.Store.Get(troop.StringId));
            }
        }

        private bool CanShowMine => IsEditGearVisible && HasOverride;

        [DataSourceMethod]
        public void ExecuteEditGear()
        {
            try
            {
                if (_troop != null)
                    GearEditorWindow.Open(_troop);
            }
            catch (Exception e)
            {
                ModLog.Error("editor", "Edit gear button", e);
            }
        }

        [DataSourceMethod]
        public void ExecuteShowVanilla()
        {
            _wantMine = false;
            ApplyView(force: false);
        }

        [DataSourceMethod]
        public void ExecuteShowMine()
        {
            _wantMine = true;
            ApplyView(force: false);
        }

        private void OnEditorClosed(CharacterObject troop)
        {
            if (troop != _troop)
                return;
            ApplyView(force: true);
        }

        /// <summary>Brings the page's sets in line with the choice; force = rebuild My version with new edits.</summary>
        private void ApplyView(bool force)
        {
            try
            {
                switch (UnitPageView.Decide(_wantMine, CanShowMine, _shownMine, force))
                {
                    case UnitPageAction.ShowMine:
                        _shownMine = ShowSets(mine: true);
                        break;
                    case UnitPageAction.ShowVanilla:
                        ShowSets(mine: false);
                        _shownMine = false;
                        break;
                }
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("unit page view", e);
            }
            OnPropertyChanged(nameof(EditGearText));
            OnPropertyChanged(nameof(IsViewToggleVisible));
            OnPropertyChanged(nameof(IsMyVersionShown));
            OnPropertyChanged(nameof(IsVanillaShown));
        }

        /// <summary>Rebuilds the page's set selector the way vanilla RefreshValues does - one entry per distinct battle
        /// set - from the troop's own sets (vanilla) or from override-applied CLONES of them (mine). The troop's sets
        /// are never written. Selecting through the selector runs the page's own OnEquipmentSetChange, which updates
        /// the tableau, the equipment icon lists and the "Set i/n" text. Returns false if nothing was rebuilt.</summary>
        private bool ShowSets(bool mine)
        {
            var selector = ViewModel?.EquipmentSetSelector;
            if (_troop == null || selector == null)
                return false;
            var over = mine ? GearBehavior.Instance?.Store.Get(_troop.StringId) : null;
            if (mine && over == null)
                return false;

            // Set i of the troop with set i's own edits (per-set editing); vanilla = the troop's own objects, read only.
            var sets = GearApplier.BattleSets(_troop).Select((e, i) =>
            {
                if (over == null) return e;
                var clone = e.Clone();
                GearApplier.Apply(over.GetSet(i), clone);
                return clone;
            });
            var distinct = UnitPageView.DistinctInOrder(sets, (a, b) => a.IsEquipmentEqualTo(b));
            if (distinct.Count == 0)
                return false;

            var onChange = OnChangeField?.Invoke(selector);
            if (onChange == null)
                return false;
            var index = UnitPageView.KeepIndex(selector.SelectedIndex, distinct.Count);
            selector.Refresh(distinct.Select(e => new EncyclopediaUnitEquipmentSetSelectorItemVM(e)), index, onChange);
            return true;
        }

        private static AccessTools.FieldRef<EncyclopediaUnitPageVM, CharacterObject>? TryField()
        {
            try
            {
                return AccessTools.FieldRefAccess<EncyclopediaUnitPageVM, CharacterObject>("_character");
            }
            catch (Exception e)
            {
                ModLog.Error("load", "EncyclopediaUnitPageVM._character not found - no Edit gear button", e);
                return null;
            }
        }

        private static AccessTools.FieldRef<SelectorVM<EncyclopediaUnitEquipmentSetSelectorItemVM>, Action<SelectorVM<EncyclopediaUnitEquipmentSetSelectorItemVM>>>? TryOnChangeField()
        {
            try
            {
                return AccessTools.FieldRefAccess<SelectorVM<EncyclopediaUnitEquipmentSetSelectorItemVM>, Action<SelectorVM<EncyclopediaUnitEquipmentSetSelectorItemVM>>>("_onChange");
            }
            catch (Exception e)
            {
                ModLog.Error("load", "SelectorVM._onChange not found - the unit page stays vanilla", e);
                return null;
            }
        }
    }
}

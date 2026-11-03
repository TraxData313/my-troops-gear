using System;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace MyTroopsGear.UI
{
    // The bar of our buttons on the Encyclopedia unit page: a row just above the vanilla equipment-set arrows, inside the
    // left (tableau) column, inserted as the next sibling of the vanilla BookmarkButton. "Edit gear" now; step 5 adds the
    // Vanilla / My version toggle into the same ListPanel (Id MyTroopsGearBar), in front of it.
    [PrefabExtension("EncyclopediaUnitPage", "descendant::ButtonWidget[@Id='BookmarkButton']")]
    public sealed class EncyclopediaUnitPageBarPrefab : PrefabExtensionInsertPatch
    {
        public override InsertType Type => InsertType.Append;

        [PrefabExtensionText]
        public string Content => @"
<ListPanel Id=""MyTroopsGearBar"" WidthSizePolicy=""CoverChildren"" HeightSizePolicy=""CoverChildren"" HorizontalAlignment=""Center"" VerticalAlignment=""Bottom"" MarginBottom=""104"" StackLayout.LayoutMethod=""HorizontalLeftToRight"" IsVisible=""@IsEditGearVisible"">
  <Children>
    <ButtonWidget Id=""MyTroopsGearEditButton"" DoNotPassEventsToChildren=""true"" WidthSizePolicy=""Fixed"" HeightSizePolicy=""Fixed"" SuggestedWidth=""170"" SuggestedHeight=""40"" Brush=""ButtonBrush2"" Command.Click=""ExecuteEditGear"" UpdateChildrenStates=""true"">
      <Children>
        <TextWidget DoNotAcceptEvents=""true"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Brush=""Popup.Button.Text"" Brush.FontSize=""18"" Text=""@EditGearText"" />
        <HintWidget DataSource=""{EditGearHint}"" WidthSizePolicy=""StretchToParent"" HeightSizePolicy=""StretchToParent"" Command.HoverBegin=""ExecuteBeginHint"" Command.HoverEnd=""ExecuteEndHint"" IsDisabled=""true"" />
      </Children>
    </ButtonWidget>
  </Children>
</ListPanel>";
    }

    [ViewModelMixin]
    public sealed class EncyclopediaUnitPageMixin : BaseViewModelMixin<EncyclopediaUnitPageVM>
    {
        private static readonly AccessTools.FieldRef<EncyclopediaUnitPageVM, CharacterObject>? CharacterField = TryField();

        private readonly CharacterObject? _troop;

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
        }

        public override void OnFinalize()
        {
            GearEditorWindow.EditorClosed -= OnEditorClosed;
            base.OnFinalize();
        }

        [DataSourceProperty]
        public bool IsEditGearVisible => _troop != null && !_troop.IsHero && Campaign.Current != null && GearBehavior.Instance != null;

        [DataSourceProperty]
        public string EditGearText => GearBehavior.Instance?.Store.HasOverride(_troop?.StringId) == true ? "Edit gear *" : "Edit gear";

        [DataSourceProperty]
        public HintViewModel EditGearHint { get; } = EditorColors.Hint(
            "Change this troop's battle gear, slot by slot. Free and instant; only troops of your own parties wear it. "
            + "* = you have edited this troop.");

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

        private void OnEditorClosed(CharacterObject troop)
        {
            if (troop == _troop)
                OnPropertyChanged(nameof(EditGearText));
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
    }
}

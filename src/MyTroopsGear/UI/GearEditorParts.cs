using System;
using System.Collections.Generic;
using MyTroopsGear.Core;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace MyTroopsGear.UI
{
    internal static class EditorColors
    {
        public const string Edited = "#F2D58CFF";  // gold: our item
        public const string Vanilla = "#9C968CFF"; // grey: the troop's own
        public const string Label = "#D9CBB4FF";
        public const string Warning = "#E8A15AFF";

        public static HintViewModel Hint(string text) => new HintViewModel(new TextObject("{=!}" + text));

        public static void ShowItemTooltip(ItemObject? item)
        {
            if (item == null)
                return;
            try
            {
                InformationManager.ShowTooltip(typeof(ItemObject), new EquipmentElement(item));
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("item tooltip", e);
            }
        }

        public static void HideTooltip()
        {
            try
            {
                MBInformationManager.HideInformations();
            }
            catch
            {
                // nothing shown
            }
        }
    }

    /// <summary>A "[&lt;] value [&gt;]" filter of the picker: a fixed list of options, stepped round.</summary>
    internal sealed class CycleFilterVM : ViewModel
    {
        private readonly Action _onChange;
        private List<KeyValuePair<string, object?>> _options = new List<KeyValuePair<string, object?>>();
        private int _index;
        private string _valueText = "";

        public CycleFilterVM(string label, Action onChange)
        {
            LabelText = label;
            _onChange = onChange;
        }

        [DataSourceProperty] public string LabelText { get; }

        [DataSourceProperty]
        public string ValueText
        {
            get => _valueText;
            private set
            {
                if (value == _valueText) return;
                _valueText = value;
                OnPropertyChangedWithValue(value, nameof(ValueText));
            }
        }

        public object? Value => _index >= 0 && _index < _options.Count ? _options[_index].Value : null;

        /// <summary>New options; the selection goes to the option whose value equals <paramref name="keep"/>, else
        /// to <paramref name="fallbackIndex"/>. Does not raise the change callback.</summary>
        public void SetOptions(List<KeyValuePair<string, object?>> options, object? keep, int fallbackIndex = 0)
        {
            _options = options;
            _index = Math.Max(0, Math.Min(options.Count - 1, fallbackIndex));
            for (int i = 0; i < options.Count; i++)
            {
                if (Equals(options[i].Value, keep))
                {
                    _index = i;
                    break;
                }
            }
            ValueText = _options.Count > 0 ? _options[_index].Key : "";
        }

        public void ExecutePrevious() => Step(-1);

        public void ExecuteNext() => Step(1);

        private void Step(int by)
        {
            if (_options.Count < 2) return;
            _index = (_index + by + _options.Count) % _options.Count;
            ValueText = _options[_index].Key;
            _onChange();
        }
    }

    /// <summary>One row of the slot list: the slot, what it holds, [Change] and [Reset].</summary>
    internal sealed class GearSlotVM : ViewModel
    {
        private readonly GearEditorVM _owner;
        private ItemObject? _item;
        private string _itemText = "";
        private string _itemColor = EditorColors.Vanilla;
        private string _statusText = "";
        private bool _isOverridden;
        private bool _isSelected;
        private bool _hasItem;
        private string _priceText = "";
        private ItemImageIdentifierVM _visual = new ItemImageIdentifierVM(null);

        public GearSlotVM(GearEditorVM owner, int slot)
        {
            _owner = owner;
            Slot = slot;
            SlotText = GearEditing.Label(slot);
            ResetHint = EditorColors.Hint(GearSlots.BlockOf(slot).Count > 1
                ? "Back to this troop's own item for this slot. When every " + (GearSlots.IsWeapon(slot) ? "weapon" : "mount")
                  + " slot is back on its own item, the troop rolls its vanilla sets again."
                : "Back to vanilla: this slot rolls the troop's own sets again.");
        }

        public int Slot { get; }

        [DataSourceProperty] public string SlotText { get; }

        [DataSourceProperty] public string ChangeText => "Change";

        [DataSourceProperty] public string ResetText => "Reset";

        [DataSourceProperty] public string VanillaTagText => "vanilla";

        [DataSourceProperty] public HintViewModel ResetHint { get; }

        [DataSourceProperty]
        public string ItemText
        {
            get => _itemText;
            set { if (value != _itemText) { _itemText = value; OnPropertyChangedWithValue(value, nameof(ItemText)); } }
        }

        [DataSourceProperty]
        public string ItemColor
        {
            get => _itemColor;
            set { if (value != _itemColor) { _itemColor = value; OnPropertyChangedWithValue(value, nameof(ItemColor)); } }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set { if (value != _statusText) { _statusText = value; OnPropertyChangedWithValue(value, nameof(StatusText)); } }
        }

        [DataSourceProperty]
        public bool IsOverridden
        {
            get => _isOverridden;
            set { if (value != _isOverridden) { _isOverridden = value; OnPropertyChangedWithValue(value, nameof(IsOverridden)); } }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); } }
        }

        /// <summary>"Tier 3 | 1,250" of the shown item (ours or the vanilla one); empty for an empty slot.</summary>
        [DataSourceProperty]
        public string PriceText
        {
            get => _priceText;
            set { if (value != _priceText) { _priceText = value; OnPropertyChangedWithValue(value, nameof(PriceText)); } }
        }

        [DataSourceProperty]
        public bool HasItem
        {
            get => _hasItem;
            set { if (value != _hasItem) { _hasItem = value; OnPropertyChangedWithValue(value, nameof(HasItem)); } }
        }

        [DataSourceProperty]
        public ItemImageIdentifierVM Visual
        {
            get => _visual;
            set { if (value != _visual) { _visual = value; OnPropertyChangedWithValue(value, nameof(Visual)); } }
        }

        public ItemObject? Item => _item;

        /// <summary><paramref name="edited"/>: the player's item (null = emptied), gold with Reset. Otherwise grey
        /// "vanilla: X": the shown vanilla set's item, or a block mate stored unchanged with an edited weapon/mount.</summary>
        public void Show(ItemObject? item, bool edited)
        {
            if (item != _item || Visual == null)
            {
                _item = item;
                Visual = new ItemImageIdentifierVM(item);
            }
            HasItem = item != null;
            PriceText = item == null ? "" : GearEditing.TierPriceText(ItemCandidates.TierFromGame((int)item.Tier), item.Value);
            IsOverridden = edited;
            var name = item?.Name?.ToString() ?? "(empty)";
            ItemText = edited ? name : "vanilla: " + name;
            ItemColor = edited ? EditorColors.Edited : EditorColors.Vanilla;
            StatusText = edited ? "yours" : "";
        }

        public void ExecuteChange() => _owner.OpenPicker(Slot);

        public void ExecuteReset() => _owner.RevertSlot(Slot);

        public void ExecuteBeginHint() => EditorColors.ShowItemTooltip(_item);

        public void ExecuteEndHint() => EditorColors.HideTooltip();
    }

    /// <summary>One card of the picker grid (or the "Empty" card).</summary>
    internal sealed class ItemCardVM : ViewModel
    {
        private readonly GearEditorVM _owner;
        private bool _isCurrent;

        public ItemCardVM(GearEditorVM owner, ItemObject? item, string name, string detail)
        {
            _owner = owner;
            Item = item;
            NameText = name;
            DetailText = detail;
            IsEmptyChoice = item == null;
            HasPrice = item != null;
            Visual = new ItemImageIdentifierVM(item);
        }

        public ItemObject? Item { get; }

        [DataSourceProperty] public string NameText { get; }

        [DataSourceProperty] public string DetailText { get; }

        [DataSourceProperty] public bool IsEmptyChoice { get; }

        [DataSourceProperty] public bool HasPrice { get; }

        [DataSourceProperty] public ItemImageIdentifierVM Visual { get; }

        [DataSourceProperty]
        public bool IsCurrent
        {
            get => _isCurrent;
            set { if (value != _isCurrent) { _isCurrent = value; OnPropertyChangedWithValue(value, nameof(IsCurrent)); } }
        }

        public void ExecuteSelect() => _owner.Pick(Item);

        public void ExecuteBeginHint() => EditorColors.ShowItemTooltip(Item);

        public void ExecuteEndHint() => EditorColors.HideTooltip();
    }
}

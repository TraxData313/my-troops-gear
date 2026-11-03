using System;
using System.Collections.Generic;
using System.Linq;
using MyTroopsGear.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace MyTroopsGear.UI
{
    /// <summary>
    /// The gear editor of one troop (movie <see cref="GearEditorWindow.MovieName"/>), one vanilla battle set at a time.
    /// Left: the troop on a tableau, wearing a CLONE of the edited set with that set's edits written on top; the arrows
    /// pick the set being edited (the troop's BattleEquipments in game order - the indices the store and the spawn use).
    /// Middle: one row per slot of THIS set with Change, Reset (this slot, this set) and All sets (this row's item onto
    /// that slot of every set). Right: the item picker of the slot being changed. Every edit goes straight to the campaign's store - free and instant.
    /// The troop's own CharacterObject equipment is never written to.
    /// </summary>
    internal sealed class GearEditorVM : ViewModel
    {
        public const int MaxCards = 150;
        private const float ResetConfirmSeconds = 4f;

        private readonly CharacterObject _troop;
        private readonly List<Equipment> _sets;
        private readonly Func<int, int, string?> _vanillaOf;
        private int _setIndex;
        private int? _pickerSlot;
        private Equipment _preview = new Equipment();
        private float _resetArmedFor;
        private bool _refreshingFilters;

        private string _setText = "";
        private string _warningText = "";
        private bool _hasWarning;
        private bool _canResetAll;
        private bool _canResetSet;
        private string _resetAllText = "";
        private bool _isPickerActive;
        private string _pickerTitleText = "";
        private string _resultText = "";
        private string _searchText = "";
        private string _editedSummaryText = "";

        public GearEditorVM(CharacterObject troop)
        {
            _troop = troop;
            _sets = VanillaSets(troop);
            _vanillaOf = GearApplier.VanillaOf(troop);
            TitleText = troop.Name?.ToString() ?? troop.StringId;
            SubtitleText = "Tier " + troop.Tier + " " + (troop.Culture?.Name?.ToString() ?? "") + " - "
                           + FormationName(troop.DefaultFormationClass);
            NoteText = "Edits are free and instant. Only this troop in your party, your clan's parties and your garrisons "
                       + "wears them; everyone else on the map keeps vanilla.";
            Character = new CharacterViewModel(CharacterViewModel.StanceTypes.OnMount);
            Character.FillFrom(troop);
            Slots = new MBBindingList<GearSlotVM>();
            foreach (var slot in GearEditing.EditorOrder)
                Slots.Add(new GearSlotVM(this, slot));
            Cards = new MBBindingList<ItemCardVM>();
            TierFilter = new CycleFilterVM("Tier", OnFilterChanged);
            CultureFilter = new CycleFilterVM("Culture", OnFilterChanged);
            TypeFilter = new CycleFilterVM("Type", OnFilterChanged);
            TierFilter.SetOptions(TierOptions(), ItemCandidates.DefaultMaxTier(troop.Tier));
            ResetAllHint = EditorColors.Hint("Every slot of every set of this troop back to vanilla: it rolls its own sets "
                                             + "again. Click twice to confirm.");
            ResetSetHint = EditorColors.Hint("Every slot of the set shown here back to vanilla. The other sets keep their edits.");
            ResetAllText = ResetAllIdleText;
            Refresh();
        }

        public CharacterObject Troop => _troop;

        private static string ResetAllIdleText => "Reset whole unit to vanilla";

        private static OverrideStore? Store => GearBehavior.Instance?.Store;

        // ---- bindings -----------------------------------------------------------------------------------

        [DataSourceProperty] public string TitleText { get; }

        [DataSourceProperty] public string SubtitleText { get; }

        [DataSourceProperty] public string NoteText { get; }

        [DataSourceProperty] public string CloseText => "Done";

        [DataSourceProperty] public string SlotsHeaderText => "Gear";

        [DataSourceProperty] public string PickerIdleText => "Press Change on a slot to pick its item.";

        [DataSourceProperty] public string SearchLabelText => "Search";

        [DataSourceProperty] public CharacterViewModel Character { get; }

        [DataSourceProperty] public MBBindingList<GearSlotVM> Slots { get; }

        [DataSourceProperty] public MBBindingList<ItemCardVM> Cards { get; }

        [DataSourceProperty] public CycleFilterVM TierFilter { get; }

        [DataSourceProperty] public CycleFilterVM CultureFilter { get; }

        [DataSourceProperty] public CycleFilterVM TypeFilter { get; }

        [DataSourceProperty] public HintViewModel ResetAllHint { get; }

        [DataSourceProperty] public HintViewModel ResetSetHint { get; }

        [DataSourceProperty] public string ResetSetText => "Reset this set";

        public int SetCount => _sets.Count;

        [DataSourceProperty] public bool HasManySets => _sets.Count > 1;

        [DataSourceProperty]
        public string SetText
        {
            get => _setText;
            set { if (value != _setText) { _setText = value; OnPropertyChangedWithValue(value, nameof(SetText)); } }
        }

        [DataSourceProperty]
        public string EditedSummaryText
        {
            get => _editedSummaryText;
            set { if (value != _editedSummaryText) { _editedSummaryText = value; OnPropertyChangedWithValue(value, nameof(EditedSummaryText)); } }
        }

        [DataSourceProperty]
        public string WarningText
        {
            get => _warningText;
            set { if (value != _warningText) { _warningText = value; OnPropertyChangedWithValue(value, nameof(WarningText)); } }
        }

        [DataSourceProperty]
        public bool HasWarning
        {
            get => _hasWarning;
            set { if (value != _hasWarning) { _hasWarning = value; OnPropertyChangedWithValue(value, nameof(HasWarning)); } }
        }

        [DataSourceProperty]
        public bool CanResetAll
        {
            get => _canResetAll;
            set { if (value != _canResetAll) { _canResetAll = value; OnPropertyChangedWithValue(value, nameof(CanResetAll)); } }
        }

        [DataSourceProperty]
        public bool CanResetSet
        {
            get => _canResetSet;
            set { if (value != _canResetSet) { _canResetSet = value; OnPropertyChangedWithValue(value, nameof(CanResetSet)); } }
        }

        [DataSourceProperty]
        public string ResetAllText
        {
            get => _resetAllText;
            set { if (value != _resetAllText) { _resetAllText = value; OnPropertyChangedWithValue(value, nameof(ResetAllText)); } }
        }

        [DataSourceProperty]
        public bool IsPickerActive
        {
            get => _isPickerActive;
            set { if (value != _isPickerActive) { _isPickerActive = value; OnPropertyChangedWithValue(value, nameof(IsPickerActive)); } }
        }

        [DataSourceProperty]
        public string PickerTitleText
        {
            get => _pickerTitleText;
            set { if (value != _pickerTitleText) { _pickerTitleText = value; OnPropertyChangedWithValue(value, nameof(PickerTitleText)); } }
        }

        [DataSourceProperty]
        public string ResultText
        {
            get => _resultText;
            set { if (value != _resultText) { _resultText = value; OnPropertyChangedWithValue(value, nameof(ResultText)); } }
        }

        [DataSourceProperty]
        public string SearchText
        {
            get => _searchText;
            set
            {
                value ??= "";
                if (value == _searchText) return;
                _searchText = value;
                OnPropertyChangedWithValue(value, nameof(SearchText));
                RefreshCards();
            }
        }

        // ---- commands -----------------------------------------------------------------------------------

        public void ExecuteClose() => GearEditorWindow.RequestClose();

        public void ExecutePreviousSet() => StepSet(-1);

        public void ExecuteNextSet() => StepSet(1);

        public void ExecuteClosePicker()
        {
            _pickerSlot = null;
            IsPickerActive = false;
            Cards.Clear();
            foreach (var row in Slots)
                row.IsSelected = false;
        }

        public void ExecuteClearSearch() => SearchText = "";

        /// <summary>Two clicks: the first arms it (the text asks to click again), the second resets.</summary>
        public void ExecuteResetAll()
        {
            if (!CanResetAll) return;
            if (_resetArmedFor <= 0f)
            {
                _resetArmedFor = ResetConfirmSeconds;
                ResetAllText = "Click again to reset ALL slots";
                return;
            }
            _resetArmedFor = 0f;
            ResetAllText = ResetAllIdleText;
            Edit("reset all", store => GearEditing.ResetTroop(store, _troop.StringId));
        }

        /// <summary>One click (the other sets keep their edits, so it is cheap to redo).</summary>
        public void ExecuteResetSet()
        {
            if (!CanResetSet) return;
            int set = _setIndex;
            Edit("reset set " + (set + 1), store => GearEditing.ResetSet(store, _troop.StringId, set));
        }

        public void OpenPicker(int slot)
        {
            _pickerSlot = slot;
            IsPickerActive = true;
            PickerTitleText = "Pick: " + GearEditing.Label(slot);
            foreach (var row in Slots)
                row.IsSelected = row.Slot == slot;
            RebuildFilters(slot);
            RefreshCards();
        }

        public void RevertSlot(int slot)
        {
            int set = _setIndex;
            Edit("set " + (set + 1) + " reset " + GearSlots.Name(slot),
                store => GearEditing.RevertSlot(store, _troop.StringId, set, slot, s => _vanillaOf(set, s)));
        }

        /// <summary>The row's "All sets": what this set wears in <paramref name="slot"/> goes onto that slot of every set.</summary>
        public void ApplyToAllSets(int slot)
        {
            int set = _setIndex;
            Edit("set " + (set + 1) + " " + GearSlots.Name(slot) + " to all sets",
                store => GearEditing.ApplyToAllSets(store, _troop.StringId, set, slot, _sets.Count, _vanillaOf) > 0);
        }

        /// <summary>A picker card: <paramref name="item"/> (null = the Empty card) goes into the open slot.</summary>
        public void Pick(ItemObject? item)
        {
            if (_pickerSlot == null) return;
            int slot = _pickerSlot.Value;
            if (item != null)
            {
                var why = GearApplier.WhyNotFits(item, slot);
                if (why != null)
                {
                    SubModule.Warn(why);
                    return;
                }
            }
            var id = item?.StringId ?? GearSlots.Empty;
            int set = _setIndex;
            Edit("set " + (set + 1) + " " + GearSlots.Name(slot) + "=" + id, store =>
            {
                GearEditing.SetSlot(store, _troop.StringId, set, slot, id, s => _vanillaOf(set, s));
                return true;
            });
        }

        /// <summary>Every frame while open (the two-click reset times out).</summary>
        public void Tick(float dt)
        {
            if (_resetArmedFor <= 0f) return;
            _resetArmedFor -= dt;
            if (_resetArmedFor <= 0f)
                ResetAllText = ResetAllIdleText;
        }

        // ---- internals ----------------------------------------------------------------------------------

        private string? VanillaId(int slot) => _vanillaOf(_setIndex, slot);

        private void Edit(string what, Func<OverrideStore, bool> change)
        {
            var store = Store;
            if (store == null)
            {
                SubModule.Warn("no campaign is loaded, nothing was changed");
                return;
            }
            try
            {
                if (change(store))
                    ModLog.Info("editor", _troop.StringId + ": " + what);
            }
            catch (Exception e)
            {
                ModLog.Error("editor", _troop.StringId + ": " + what + " failed", e);
                SubModule.Warn("that change failed (see the mod log)");
            }
            Refresh();
            if (_pickerSlot != null)
                MarkCurrentCards();
        }

        private void StepSet(int by)
        {
            if (_sets.Count < 2) return;
            _setIndex = (_setIndex + by + _sets.Count) % _sets.Count;
            Refresh();
            if (_pickerSlot != null)
                MarkCurrentCards();
        }

        /// <summary>The tableau and every row from the store and the current vanilla set, at once.</summary>
        private void Refresh()
        {
            var vanilla = _sets[_setIndex];
            var over = Store?.Get(_troop.StringId);
            var setOver = over?.GetSet(_setIndex);
            _preview = vanilla.Clone();
            GearApplier.Apply(setOver, _preview);
            Character.SetEquipment(_preview);

            SetText = "Set " + (_setIndex + 1) + " / " + _sets.Count;
            foreach (var row in Slots)
            {
                if (setOver != null && setOver.TryGet(row.Slot, out var id))
                    row.Show(GearApplier.FindItem(id), GearEditing.IsSlotEdited(setOver, row.Slot, VanillaId));
                else
                    row.Show(vanilla[(EquipmentIndex)row.Slot].Item, edited: false);
                row.IsAllSetsVisible = HasManySets;
                row.CanApplyAllSets = HasManySets && GearEditing.CanApplyToAllSets(over, _setIndex, row.Slot, _sets.Count, _vanillaOf);
            }
            int edited = Slots.Count(r => r.IsOverridden);
            int total = GearEditing.EditedSlotCount(over, _vanillaOf);
            CanResetAll = over != null;
            CanResetSet = setOver != null;
            var here = edited == 0 ? "No slots edited in this set" : edited + " slot" + (edited == 1 ? "" : "s") + " edited in this set";
            EditedSummaryText = total > edited ? here + " (" + total + " in all sets)" : here;
            if (!CanResetAll)
            {
                _resetArmedFor = 0f;
                ResetAllText = ResetAllIdleText;
            }
            var warning = MountWarning(setOver);
            WarningText = warning ?? "";
            HasWarning = warning != null;
        }

        private string? MountWarning(SetOverride? over)
        {
            if (over == null || !over.TryGet(GearSlots.Horse, out var horse))
                return null;
            bool mountedClass = IsMountedClass(_troop.DefaultFormationClass);
            if (horse != GearSlots.Empty && !mountedClass)
                return "This troop is listed as " + FormationName(_troop.DefaultFormationClass)
                       + ": with a horse it still joins that formation (change it in the party screen).";
            if (horse == GearSlots.Empty && mountedClass)
                return "This troop is listed as " + FormationName(_troop.DefaultFormationClass)
                       + ": without a horse it still joins that formation.";
            return null;
        }

        private void OnFilterChanged()
        {
            if (!_refreshingFilters)
                RefreshCards();
        }

        private void RebuildFilters(int slot)
        {
            _refreshingFilters = true;
            try
            {
                var all = ItemCatalog.For(slot);
                var cultureId = _troop.Culture?.StringId;
                var cultures = new List<KeyValuePair<string, object?>>();
                if (cultureId != null)
                    cultures.Add(Option(ItemCatalog.CultureName(cultureId) + " + neutral", "and:" + cultureId));
                cultures.Add(Option("Any culture", "any"));
                cultures.Add(Option("Neutral only", "neutral"));
                foreach (var c in ItemCandidates.Cultures(all, _troop.IsFemale))
                    cultures.Add(Option(ItemCatalog.CultureName(c) + " only", "only:" + c));
                CultureFilter.SetOptions(cultures, CultureFilter.Value);

                var types = new List<KeyValuePair<string, object?>> { Option("All types", null) };
                foreach (var t in ItemCandidates.SubTypes(all, _troop.IsFemale))
                    types.Add(Option(ItemCatalog.SubTypeLabel(t), t));
                TypeFilter.SetOptions(types, null);
            }
            finally
            {
                _refreshingFilters = false;
            }
        }

        private void RefreshCards()
        {
            if (_pickerSlot == null) return;
            int slot = _pickerSlot.Value;
            try
            {
                var query = new CandidateQuery
                {
                    MaxTier = TierFilter.Value as int?,
                    SubType = TypeFilter.Value as string,
                    Search = SearchText,
                    IsFemale = _troop.IsFemale,
                };
                var culture = CultureFilter.Value as string ?? "any";
                if (culture.StartsWith("and:", StringComparison.Ordinal))
                {
                    query.Culture = CultureMode.CultureAndNeutral;
                    query.CultureId = culture.Substring(4);
                }
                else if (culture.StartsWith("only:", StringComparison.Ordinal))
                {
                    query.Culture = CultureMode.CultureOnly;
                    query.CultureId = culture.Substring(5);
                }
                else if (culture == "neutral")
                {
                    query.Culture = CultureMode.NeutralOnly;
                }

                var found = ItemCandidates.Filter(ItemCatalog.For(slot), query);
                Cards.Clear();
                Cards.Add(new ItemCardVM(this, null, "Empty", "nothing in this slot"));
                foreach (var c in found.Take(MaxCards))
                {
                    var item = ItemCatalog.Item(c.Id);
                    if (item == null) continue;
                    Cards.Add(new ItemCardVM(this, item, c.Name, GearEditing.TierPriceText(c.Tier, item.Value)));
                }
                ResultText = found.Count == 0 ? "No item matches these filters."
                    : found.Count > MaxCards ? "Showing " + MaxCards + " of " + found.Count + " - narrow the filters or search."
                    : found.Count + " item" + (found.Count == 1 ? "" : "s");
                MarkCurrentCards();
            }
            catch (Exception e)
            {
                ModLog.Error("editor", "building the picker for " + GearSlots.Name(slot), e);
                ResultText = "The item list failed to build (see the mod log).";
            }
        }

        private void MarkCurrentCards()
        {
            if (_pickerSlot == null) return;
            var current = _preview[(EquipmentIndex)_pickerSlot.Value].Item;
            foreach (var card in Cards)
                card.IsCurrent = card.Item == current;
        }

        private static KeyValuePair<string, object?> Option(string text, object? value) => new KeyValuePair<string, object?>(text, value);

        private static List<KeyValuePair<string, object?>> TierOptions()
        {
            var list = new List<KeyValuePair<string, object?>>();
            for (int t = ItemCandidates.MinTier; t <= ItemCandidates.MaxTier; t++)
                list.Add(Option("Up to tier " + t, t));
            list.Add(Option("Any tier", null));
            return list;
        }

        /// <summary>The troop's vanilla battle sets in game order, never empty. NOT de-duplicated like the Encyclopedia's
        /// list: set i here is set i in the store and at spawn, so two equal vanilla sets are edited separately.</summary>
        private static List<Equipment> VanillaSets(CharacterObject troop)
        {
            var sets = GearApplier.BattleSets(troop);
            if (sets.Count == 0)
                sets.Add(troop.FirstBattleEquipment ?? new Equipment());
            return sets;
        }

        private static bool IsMountedClass(FormationClass c) =>
            c == FormationClass.Cavalry || c == FormationClass.HorseArcher
            || c == FormationClass.LightCavalry || c == FormationClass.HeavyCavalry;

        private static string FormationName(FormationClass c)
        {
            switch (c)
            {
                case FormationClass.HorseArcher: return "horse archer";
                case FormationClass.LightCavalry: return "light cavalry";
                case FormationClass.HeavyCavalry: return "heavy cavalry";
                case FormationClass.HeavyInfantry: return "heavy infantry";
                default: return c.ToString().ToLowerInvariant();
            }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            Character?.OnFinalize();
            TierFilter.OnFinalize();
            CultureFilter.OnFinalize();
            TypeFilter.OnFinalize();
            foreach (var row in Slots)
                row.OnFinalize();
            foreach (var card in Cards)
                card.OnFinalize();
        }
    }
}

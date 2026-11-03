using System;
using System.Collections.Generic;
using System.Linq;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// Every troop override of the campaign: troop string id -> <see cref="TroopOverride"/> (set index -> slot -> item).
    /// Pure data; the campaign behaviour saves it as lines (<see cref="ToLines"/>) and the spawn hook reads it.
    ///
    /// Old saves (before per-set editing) hold <c>troopId|slot|itemId</c> lines that applied to every set. They are
    /// kept aside as "all sets" edits until <see cref="Prune"/> learns each troop's set count and copies them onto
    /// every set; until then <see cref="ToLines"/> writes them back unchanged, so nothing is lost.
    /// </summary>
    public sealed class OverrideStore
    {
        private readonly SortedDictionary<string, TroopOverride> _troops = new SortedDictionary<string, TroopOverride>(StringComparer.Ordinal);

        /// <summary>Old all-sets lines not yet expanded onto the sets (see <see cref="Prune"/>).</summary>
        private readonly SortedDictionary<string, SetOverride> _allSets = new SortedDictionary<string, SetOverride>(StringComparer.Ordinal);

        /// <summary>Raised after any change made through this class.</summary>
        public event Action? Changed;

        /// <summary>Troops with per-set edits.</summary>
        public int Count => _troops.Count;

        /// <summary>Troops whose old all-sets lines still wait for <see cref="Prune"/>.</summary>
        public int PendingAllSetsCount => _allSets.Count;

        public IEnumerable<string> TroopIds => _troops.Keys;

        public bool HasOverride(string? troopId) => troopId != null && _troops.ContainsKey(troopId);

        /// <summary>The troop's edits, or null when it has none (= fully vanilla). Do not change the returned object
        /// directly; use the methods here so empty sets are pruned.</summary>
        public TroopOverride? Get(string? troopId) =>
            troopId != null && _troops.TryGetValue(troopId, out var o) ? o : null;

        /// <summary>The edits of one set, or null when that set is vanilla.</summary>
        public SetOverride? GetSet(string? troopId, int set) => Get(troopId)?.GetSet(set);

        /// <summary>See <see cref="SetOverride.Set"/>; the block seed (this set's vanilla items) fills the rest of a new
        /// weapon block / mount pair.</summary>
        public void Set(string troopId, int set, int slot, string itemId, Func<int, string?>? blockSeed = null)
        {
            if (!TroopOverride.IsValidId(troopId))
                throw new ArgumentException("Bad troop id: '" + troopId + "'.", nameof(troopId));
            if (!TroopOverride.IsValidSet(set))
                throw new ArgumentOutOfRangeException(nameof(set), set, "Not a battle set index.");
            // Validate on a scratch copy first, so a bad slot/item never leaves an empty troop or set behind.
            new SetOverride().Set(slot, itemId);
            if (!_troops.TryGetValue(troopId, out var o))
                _troops[troopId] = o = new TroopOverride();
            o.GetOrAdd(set).Set(slot, itemId, blockSeed);
            Changed?.Invoke();
        }

        /// <summary>Back to vanilla for this slot of this set (its whole block for weapons / mount).</summary>
        public void Revert(string troopId, int set, int slot)
        {
            if (!_troops.TryGetValue(troopId, out var o)) return;
            var s = o.GetSet(set);
            if (s == null) return;
            s.Revert(slot);
            o.RemoveIfEmpty(set);
            if (o.IsEmpty) _troops.Remove(troopId);
            Changed?.Invoke();
        }

        /// <summary>One set goes fully vanilla. False if it had no edits.</summary>
        public bool ClearSet(string troopId, int set)
        {
            if (!_troops.TryGetValue(troopId, out var o) || !o.RemoveSet(set)) return false;
            if (o.IsEmpty) _troops.Remove(troopId);
            Changed?.Invoke();
            return true;
        }

        /// <summary>The troop goes fully vanilla (every set). Returns false if it had no override.</summary>
        public bool Clear(string troopId)
        {
            bool removed = _troops.Remove(troopId) | _allSets.Remove(troopId);
            if (removed) Changed?.Invoke();
            return removed;
        }

        public void ClearAll()
        {
            if (_troops.Count == 0 && _allSets.Count == 0) return;
            _troops.Clear();
            _allSets.Clear();
            Changed?.Invoke();
        }

        // ---- save format ------------------------------------------------------------------------------

        public const char Separator = '|';

        /// <summary>One line per stored slot, <c>troopId|set|slot|itemId</c>, sorted by troop, set, slot. Old all-sets
        /// lines that were never expanded are written back as <c>troopId|slot|itemId</c>.</summary>
        public List<string> ToLines()
        {
            var lines = new List<string>();
            foreach (var troop in _troops)
                foreach (var set in troop.Value.Sets)
                    foreach (var slot in set.Value.Slots)
                        lines.Add(troop.Key + Separator + set.Key + Separator + slot.Key + Separator + slot.Value);
            foreach (var troop in _allSets)
                foreach (var slot in troop.Value.Slots)
                    lines.Add(troop.Key + Separator + slot.Key + Separator + slot.Value);
            return lines;
        }

        /// <summary>
        /// Rebuilds a store from saved lines: <c>troopId|set|slot|itemId</c>, and old <c>troopId|slot|itemId</c> lines
        /// as all-sets edits. Tolerant: null/blank/malformed lines, bad sets, slots and ids are skipped and counted in
        /// <paramref name="skipped"/>; for a repeated line key the last line wins; a weapon block or mount pair that
        /// lost lines is completed with Empty.
        /// </summary>
        public static OverrideStore FromLines(IEnumerable<string?>? lines, out int skipped)
        {
            var store = new OverrideStore();
            skipped = 0;
            if (lines == null) return store;
            foreach (var raw in lines)
            {
                if (!TryParseLine(raw, out var troopId, out var set, out var slot, out var itemId))
                {
                    skipped++;
                    continue;
                }
                if (set < 0)
                {
                    if (!store._allSets.TryGetValue(troopId, out var all))
                        store._allSets[troopId] = all = new SetOverride();
                    all.SetRaw(slot, itemId);
                    continue;
                }
                if (!store._troops.TryGetValue(troopId, out var o))
                    store._troops[troopId] = o = new TroopOverride();
                o.GetOrAdd(set).SetRaw(slot, itemId);
            }
            foreach (var o in store._troops.Values)
                foreach (var s in o.Sets)
                    s.Value.CompleteBlocks();
            foreach (var s in store._allSets.Values)
                s.CompleteBlocks();
            return store;
        }

        public static OverrideStore FromLines(IEnumerable<string?>? lines) => FromLines(lines, out _);

        /// <summary>Parses one save line. <paramref name="set"/> is -1 for an old all-sets line (three fields).</summary>
        public static bool TryParseLine(string? line, out string troopId, out int set, out int slot, out string itemId)
        {
            troopId = itemId = "";
            set = slot = -1;
            if (string.IsNullOrWhiteSpace(line)) return false;
            var parts = line!.Trim().Split(Separator);
            if (parts.Length != 3 && parts.Length != 4) return false;
            var t = parts[0].Trim();
            var i = parts[parts.Length - 1].Trim();
            if (!TroopOverride.IsValidId(t) || !TroopOverride.IsValidId(i)) return false;
            if (!int.TryParse(parts[parts.Length - 2].Trim(), out var s) || !GearSlots.IsValid(s)) return false;
            int k = -1;
            if (parts.Length == 4 && (!int.TryParse(parts[1].Trim(), out k) || !TroopOverride.IsValidSet(k))) return false;
            troopId = t;
            set = k;
            slot = s;
            itemId = i;
            return true;
        }

        /// <summary>
        /// Load-time clean-up against the running game, in this order:
        /// troops <paramref name="troopExists"/> rejects are dropped whole; old all-sets edits are copied onto every set
        /// the troop has (<paramref name="setCount"/>; a set that also has per-set lines keeps those on top); sets the
        /// troop no longer has (another mod or a game update shrank its sets) are dropped; an unknown item reverts its
        /// block (all weapons / the mount pair / that armour slot) of that set to vanilla rather than leaving the troop
        /// half-armed. Returns a line per change, for the log.
        /// </summary>
        public List<string> Prune(Func<string, bool> troopExists, Func<string, bool> itemExists, Func<string, int> setCount)
        {
            var report = new List<string>();
            foreach (var troopId in _troops.Keys.Union(_allSets.Keys).ToList())
            {
                if (!troopExists(troopId))
                {
                    _troops.Remove(troopId);
                    _allSets.Remove(troopId);
                    report.Add("troop '" + troopId + "' no longer exists - its override was dropped");
                    continue;
                }
                int count = Math.Max(0, setCount(troopId));

                if (_allSets.TryGetValue(troopId, out var all))
                {
                    _allSets.Remove(troopId);
                    if (!_troops.TryGetValue(troopId, out var o0))
                        _troops[troopId] = o0 = new TroopOverride();
                    for (int k = 0; k < count && k < TroopOverride.MaxSets; k++)
                    {
                        var merged = all.Clone();
                        var own = o0.GetSet(k);
                        if (own != null) merged.OverlayWith(own);
                        o0.GetOrAdd(k).OverlayWith(merged);
                    }
                    o0.RemoveEmptySets();
                    report.Add("troop '" + troopId + "': old all-sets edits copied onto its " + count + " set(s)");
                }

                if (!_troops.TryGetValue(troopId, out var o))
                    continue;
                var gone = o.DropSetsFrom(count);
                if (gone.Count > 0)
                    report.Add("troop '" + troopId + "' has only " + count + " set(s) now - edits of set(s) "
                               + string.Join(", ", gone.Select(g => (g + 1).ToString())) + " were dropped");

                foreach (var set in o.Sets.ToList())
                {
                    var unknown = set.Value.Slots.Where(kv => kv.Value != GearSlots.Empty && !itemExists(kv.Value))
                        .Select(kv => GearSlots.Name(kv.Key) + "=" + kv.Value).ToList();
                    if (unknown.Count == 0) continue;
                    set.Value.DropUnknownItems(itemExists);
                    report.Add("troop '" + troopId + "' set " + (set.Key + 1) + ": unknown item(s) " + string.Join(", ", unknown)
                               + " - those slots are vanilla again");
                }
                o.RemoveEmptySets();
                if (o.IsEmpty) _troops.Remove(troopId);
            }
            if (report.Count > 0) Changed?.Invoke();
            return report;
        }
    }
}

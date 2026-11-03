using System;
using System.Collections.Generic;
using System.Linq;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// Every troop override of the campaign: troop string id -> <see cref="TroopOverride"/>.
    /// Pure data; the campaign behaviour saves it as lines (<see cref="ToLines"/>) and the spawn hook reads it.
    /// </summary>
    public sealed class OverrideStore
    {
        private readonly SortedDictionary<string, TroopOverride> _troops = new SortedDictionary<string, TroopOverride>(StringComparer.Ordinal);

        /// <summary>Raised after any change made through this class.</summary>
        public event Action? Changed;

        public int Count => _troops.Count;

        public IEnumerable<string> TroopIds => _troops.Keys;

        public bool HasOverride(string? troopId) => troopId != null && _troops.ContainsKey(troopId);

        /// <summary>The troop's override, or null when it has none (= fully vanilla). Do not change the
        /// returned object directly; use <see cref="Set"/> / <see cref="Revert"/> so empties are pruned.</summary>
        public TroopOverride? Get(string? troopId) =>
            troopId != null && _troops.TryGetValue(troopId, out var o) ? o : null;

        /// <summary>See <see cref="TroopOverride.Set"/>; the block seed fills the rest of a new weapon block / mount pair.</summary>
        public void Set(string troopId, int slot, string itemId, Func<int, string?>? blockSeed = null)
        {
            if (!TroopOverride.IsValidId(troopId))
                throw new ArgumentException("Bad troop id: '" + troopId + "'.", nameof(troopId));
            if (!_troops.TryGetValue(troopId, out var o))
            {
                o = new TroopOverride();
                o.Set(slot, itemId, blockSeed); // throws before the troop is added if slot/item are bad
                _troops[troopId] = o;
            }
            else
            {
                o.Set(slot, itemId, blockSeed);
            }
            Changed?.Invoke();
        }

        /// <summary>Back to vanilla for this slot (its whole block for weapons / mount).</summary>
        public void Revert(string troopId, int slot)
        {
            if (!_troops.TryGetValue(troopId, out var o)) return;
            o.Revert(slot);
            if (o.IsEmpty) _troops.Remove(troopId);
            Changed?.Invoke();
        }

        /// <summary>The troop goes fully vanilla. Returns false if it had no override.</summary>
        public bool Clear(string troopId)
        {
            bool removed = _troops.Remove(troopId);
            if (removed) Changed?.Invoke();
            return removed;
        }

        public void ClearAll()
        {
            if (_troops.Count == 0) return;
            _troops.Clear();
            Changed?.Invoke();
        }

        // ---- save format ------------------------------------------------------------------------------

        public const char Separator = '|';

        /// <summary>One line per stored slot, <c>troopId|slot|itemId</c>, sorted by troop then slot.</summary>
        public List<string> ToLines()
        {
            var lines = new List<string>();
            foreach (var troop in _troops)
                foreach (var slot in troop.Value.Slots)
                    lines.Add(troop.Key + Separator + slot.Key + Separator + slot.Value);
            return lines;
        }

        /// <summary>
        /// Rebuilds a store from saved lines. Tolerant: null/blank/malformed lines, bad slots and bad ids are
        /// skipped and counted in <paramref name="skipped"/>; for a repeated troop+slot the last line wins;
        /// a weapon block or mount pair that lost lines is completed with Empty.
        /// </summary>
        public static OverrideStore FromLines(IEnumerable<string?>? lines, out int skipped)
        {
            var store = new OverrideStore();
            skipped = 0;
            if (lines == null) return store;
            foreach (var raw in lines)
            {
                if (!TryParseLine(raw, out var troopId, out var slot, out var itemId))
                {
                    skipped++;
                    continue;
                }
                if (!store._troops.TryGetValue(troopId, out var o))
                    store._troops[troopId] = o = new TroopOverride();
                o.SetRaw(slot, itemId);
            }
            foreach (var o in store._troops.Values)
                o.CompleteBlocks();
            return store;
        }

        public static OverrideStore FromLines(IEnumerable<string?>? lines) => FromLines(lines, out _);

        public static bool TryParseLine(string? line, out string troopId, out int slot, out string itemId)
        {
            troopId = itemId = "";
            slot = -1;
            if (string.IsNullOrWhiteSpace(line)) return false;
            var parts = line!.Trim().Split(Separator);
            if (parts.Length != 3) return false;
            var t = parts[0].Trim();
            var i = parts[2].Trim();
            if (!TroopOverride.IsValidId(t) || !TroopOverride.IsValidId(i)) return false;
            if (!int.TryParse(parts[1].Trim(), out var s) || !GearSlots.IsValid(s)) return false;
            troopId = t;
            slot = s;
            itemId = i;
            return true;
        }

        /// <summary>
        /// Load-time clean-up against the running game: troops <paramref name="troopExists"/> rejects are
        /// dropped whole; an unknown item reverts its block (all weapons / the mount pair / that armour slot)
        /// to vanilla rather than leaving the troop half-armed. Returns a line per removal, for the log.
        /// </summary>
        public List<string> Prune(Func<string, bool> troopExists, Func<string, bool> itemExists)
        {
            var report = new List<string>();
            foreach (var troopId in _troops.Keys.ToList())
            {
                if (!troopExists(troopId))
                {
                    _troops.Remove(troopId);
                    report.Add("troop '" + troopId + "' no longer exists - its override was dropped");
                    continue;
                }
                var o = _troops[troopId];
                var unknown = o.Slots.Where(kv => kv.Value != GearSlots.Empty && !itemExists(kv.Value))
                    .Select(kv => GearSlots.Name(kv.Key) + "=" + kv.Value).ToList();
                if (unknown.Count == 0) continue;
                o.DropUnknownItems(itemExists);
                report.Add("troop '" + troopId + "': unknown item(s) " + string.Join(", ", unknown) + " - those slots are vanilla again");
                if (o.IsEmpty) _troops.Remove(troopId);
            }
            if (report.Count > 0) Changed?.Invoke();
            return report;
        }
    }
}

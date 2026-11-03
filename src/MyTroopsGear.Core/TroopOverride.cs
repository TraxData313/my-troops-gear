using System.Collections.Generic;
using System.Linq;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// One troop's edits: vanilla battle set index (0-based, the order of the troop's BattleEquipments) ->
    /// <see cref="SetOverride"/>. A set that is absent is worn exactly as vanilla. Never holds an empty set.
    /// </summary>
    public sealed class TroopOverride
    {
        /// <summary>Set indices a save line may carry (vanilla troops have 1-6 sets; this only stops garbage).</summary>
        public const int MaxSets = 64;

        private readonly SortedDictionary<int, SetOverride> _sets = new SortedDictionary<int, SetOverride>();

        public bool IsEmpty => _sets.Count == 0;

        /// <summary>The edited sets in set order.</summary>
        public IEnumerable<KeyValuePair<int, SetOverride>> Sets => _sets;

        public IEnumerable<int> SetIndices => _sets.Keys;

        /// <summary>The edits of set <paramref name="set"/>, or null when that set is vanilla.</summary>
        public SetOverride? GetSet(int set) => _sets.TryGetValue(set, out var s) ? s : null;

        public static bool IsValidSet(int set) => set >= 0 && set < MaxSets;

        public TroopOverride Clone()
        {
            var copy = new TroopOverride();
            foreach (var kv in _sets)
                copy._sets[kv.Key] = kv.Value.Clone();
            return copy;
        }

        internal SetOverride GetOrAdd(int set)
        {
            if (!_sets.TryGetValue(set, out var s))
                _sets[set] = s = new SetOverride();
            return s;
        }

        internal void RemoveIfEmpty(int set)
        {
            if (_sets.TryGetValue(set, out var s) && s.IsEmpty)
                _sets.Remove(set);
        }

        internal void RemoveEmptySets()
        {
            foreach (var set in _sets.Where(kv => kv.Value.IsEmpty).Select(kv => kv.Key).ToList())
                _sets.Remove(set);
        }

        internal bool RemoveSet(int set) => _sets.Remove(set);

        /// <summary>Drops every set with index &gt;= <paramref name="count"/>; returns the dropped indices.</summary>
        internal List<int> DropSetsFrom(int count)
        {
            var gone = _sets.Keys.Where(k => k >= count).ToList();
            foreach (var k in gone)
                _sets.Remove(k);
            return gone;
        }

        /// <summary>A troop or item id we can store: non-empty, no '|' (the line separator), no whitespace.</summary>
        public static bool IsValidId(string? id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var c in id!)
                if (c == '|' || char.IsWhiteSpace(c) || char.IsControl(c))
                    return false;
            return true;
        }
    }
}

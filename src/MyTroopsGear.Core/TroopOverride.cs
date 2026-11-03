using System;
using System.Collections.Generic;
using System.Linq;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// One troop's edited battle gear: slot -> item string id, or <see cref="GearSlots.Empty"/> for a slot
    /// emptied on purpose. A slot that is absent keeps the vanilla random roll.
    ///
    /// Block rule (always true after any public call): the four weapon slots are either all present or all
    /// absent, and so are horse + harness. A rolled vanilla set can otherwise mix a bow with no arrows, or a
    /// harness with no horse.
    /// </summary>
    public sealed class TroopOverride
    {
        private readonly SortedDictionary<int, string> _slots = new SortedDictionary<int, string>();

        public bool IsEmpty => _slots.Count == 0;

        /// <summary>The stored slots in slot order, whole blocks included.</summary>
        public IEnumerable<KeyValuePair<int, string>> Slots => _slots;

        public bool HasWeaponBlock => _slots.ContainsKey(GearSlots.Weapon0);

        public bool HasMountPair => _slots.ContainsKey(GearSlots.Horse);

        public bool IsOverridden(int slot) => _slots.ContainsKey(slot);

        public bool TryGet(int slot, out string itemId)
        {
            if (_slots.TryGetValue(slot, out var id))
            {
                itemId = id;
                return true;
            }
            itemId = "";
            return false;
        }

        /// <summary>
        /// Overrides <paramref name="slot"/> with <paramref name="itemId"/> (or <see cref="GearSlots.Empty"/>).
        /// If this starts a new weapon block or mount pair, the other slots of the block are filled from
        /// <paramref name="blockSeed"/> (typically the vanilla first battle set, so the troop keeps its other
        /// weapons), or with Empty where the seed gives nothing.
        /// </summary>
        public void Set(int slot, string itemId, Func<int, string?>? blockSeed = null)
        {
            if (!GearSlots.IsValid(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Not an overridable battle slot.");
            if (!IsValidId(itemId))
                throw new ArgumentException("Bad item id: '" + itemId + "'.", nameof(itemId));

            var block = GearSlots.BlockOf(slot);
            if (block.Count > 1 && !_slots.ContainsKey(slot))
            {
                foreach (var other in block)
                {
                    if (other == slot) continue;
                    var seeded = blockSeed?.Invoke(other);
                    _slots[other] = IsValidId(seeded) ? seeded! : GearSlots.Empty;
                }
            }
            _slots[slot] = itemId;
        }

        /// <summary>Back to vanilla for <paramref name="slot"/> - and for its whole block, since a block is
        /// overridden as one.</summary>
        public void Revert(int slot)
        {
            foreach (var s in GearSlots.BlockOf(slot))
                _slots.Remove(s);
        }

        public void Clear() => _slots.Clear();

        public TroopOverride Clone()
        {
            var copy = new TroopOverride();
            foreach (var kv in _slots)
                copy._slots[kv.Key] = kv.Value;
            return copy;
        }

        /// <summary>Raw write for the save reader: no block filling. Call <see cref="CompleteBlocks"/> after.</summary>
        internal void SetRaw(int slot, string itemId) => _slots[slot] = itemId;

        /// <summary>A block present only in part (lines lost) is completed with Empty, keeping the rest.</summary>
        internal void CompleteBlocks()
        {
            foreach (var block in new[] { GearSlots.WeaponBlock, GearSlots.MountPair })
            {
                if (!block.Any(_slots.ContainsKey)) continue;
                foreach (var s in block)
                    if (!_slots.ContainsKey(s))
                        _slots[s] = GearSlots.Empty;
            }
        }

        /// <summary>Drops every block that holds an item <paramref name="itemExists"/> rejects: an unknown
        /// weapon reverts all four weapons to vanilla, an unknown armour piece only its slot.</summary>
        internal void DropUnknownItems(Func<string, bool> itemExists)
        {
            var bad = _slots.Where(kv => kv.Value != GearSlots.Empty && !itemExists(kv.Value)).Select(kv => kv.Key).ToList();
            foreach (var slot in bad)
                Revert(slot);
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

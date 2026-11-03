using System;
using System.Collections.Generic;
using System.Linq;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// The edited gear of ONE vanilla battle set of a troop: slot -> item string id, or <see cref="GearSlots.Empty"/>
    /// for a slot emptied on purpose. A slot that is absent keeps that set's vanilla item.
    ///
    /// Block rule (always true after any public call): the four weapon slots are either all present or all
    /// absent, and so are horse + harness. A loadout can otherwise mix a bow with no arrows, or a harness with
    /// no horse.
    /// </summary>
    public sealed class SetOverride
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
        /// <paramref name="blockSeed"/> (typically this set's vanilla items, so the troop keeps its other
        /// weapons), or with Empty where the seed gives nothing.
        /// </summary>
        public void Set(int slot, string itemId, Func<int, string?>? blockSeed = null)
        {
            if (!GearSlots.IsValid(slot))
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Not an overridable battle slot.");
            if (!TroopOverride.IsValidId(itemId))
                throw new ArgumentException("Bad item id: '" + itemId + "'.", nameof(itemId));

            var block = GearSlots.BlockOf(slot);
            if (block.Count > 1 && !_slots.ContainsKey(slot))
            {
                foreach (var other in block)
                {
                    if (other == slot) continue;
                    var seeded = blockSeed?.Invoke(other);
                    _slots[other] = TroopOverride.IsValidId(seeded) ? seeded! : GearSlots.Empty;
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

        public SetOverride Clone()
        {
            var copy = new SetOverride();
            foreach (var kv in _slots)
                copy._slots[kv.Key] = kv.Value;
            return copy;
        }

        /// <summary>Raw write for the save reader: no block filling. Call <see cref="CompleteBlocks"/> after.</summary>
        internal void SetRaw(int slot, string itemId) => _slots[slot] = itemId;

        /// <summary>Copies every stored slot of <paramref name="other"/> over this one (whole blocks, since both
        /// keep the block rule).</summary>
        internal void OverlayWith(SetOverride other)
        {
            foreach (var kv in other._slots)
                _slots[kv.Key] = kv.Value;
        }

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
    }
}

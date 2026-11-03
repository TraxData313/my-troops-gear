using System.Collections.Generic;

namespace MyTroopsGear.Core
{
    /// <summary>
    /// The battle-equipment slots an override may touch. The numbers are the game's own
    /// <c>TaleWorlds.Core.EquipmentIndex</c> values (Core has no game reference, so they are repeated here,
    /// and the module checks them against the enum). Slot 4 (ExtraWeaponSlot, the banner) is never overridden.
    /// </summary>
    public static class GearSlots
    {
        public const int Weapon0 = 0;
        public const int Weapon1 = 1;
        public const int Weapon2 = 2;
        public const int Weapon3 = 3;
        public const int Head = 5;
        public const int Body = 6;
        public const int Leg = 7;
        public const int Gloves = 8;
        public const int Cape = 9;
        public const int Horse = 10;
        public const int Harness = 11;

        /// <summary>The marker for a slot the player emptied on purpose (stored as an item id).</summary>
        public const string Empty = "empty";

        /// <summary>Every overridable slot, in the game's order.</summary>
        public static readonly IReadOnlyList<int> All = new[] { Weapon0, Weapon1, Weapon2, Weapon3, Head, Body, Leg, Gloves, Cape, Horse, Harness };

        public static readonly IReadOnlyList<int> WeaponBlock = new[] { Weapon0, Weapon1, Weapon2, Weapon3 };
        public static readonly IReadOnlyList<int> MountPair = new[] { Horse, Harness };

        public static bool IsValid(int slot) => (slot >= Weapon0 && slot <= Weapon3) || (slot >= Head && slot <= Harness);

        public static bool IsWeapon(int slot) => slot >= Weapon0 && slot <= Weapon3;

        public static bool IsMount(int slot) => slot == Horse || slot == Harness;

        /// <summary>The slots that are stored and applied together with <paramref name="slot"/>:
        /// all four weapons, horse + harness, or the armour slot alone.</summary>
        public static IReadOnlyList<int> BlockOf(int slot)
        {
            if (IsWeapon(slot)) return WeaponBlock;
            if (IsMount(slot)) return MountPair;
            return new[] { slot };
        }

        /// <summary>Short names for logs and the console (also accepted as input).</summary>
        public static string Name(int slot)
        {
            switch (slot)
            {
                case Weapon0: return "weapon0";
                case Weapon1: return "weapon1";
                case Weapon2: return "weapon2";
                case Weapon3: return "weapon3";
                case Head: return "head";
                case Body: return "body";
                case Leg: return "leg";
                case Gloves: return "gloves";
                case Cape: return "cape";
                case Horse: return "horse";
                case Harness: return "harness";
                default: return slot.ToString();
            }
        }

        /// <summary>Accepts a slot number (0-3, 5-11) or a name from <see cref="Name"/>.</summary>
        public static bool TryParse(string? text, out int slot)
        {
            slot = -1;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text!.Trim().ToLowerInvariant();
            if (int.TryParse(text, out var n))
            {
                slot = n;
                return IsValid(n);
            }
            foreach (var s in All)
            {
                if (Name(s) == text)
                {
                    slot = s;
                    return true;
                }
            }
            return false;
        }
    }
}

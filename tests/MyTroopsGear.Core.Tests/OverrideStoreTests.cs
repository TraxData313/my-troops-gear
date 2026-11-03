using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

public class BlockRuleTests
{
    private static readonly Dictionary<int, string> Vanilla = new()
    {
        [GearSlots.Weapon0] = "spear_a",
        [GearSlots.Weapon1] = "shield_a",
        // weapon2, weapon3 empty in vanilla
        [GearSlots.Horse] = "horse_a",
        [GearSlots.Harness] = "harness_a",
    };

    private static string? Seed(int slot) => Vanilla.TryGetValue(slot, out var id) ? id : null;

    [Fact]
    public void Armour_slot_is_independent()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Head, "helmet_x", Seed);
        Assert.Single(o.Slots);
        Assert.True(o.IsOverridden(GearSlots.Head));
        Assert.False(o.HasWeaponBlock);
    }

    [Fact]
    public void First_weapon_edit_takes_the_whole_block_seeded_from_vanilla()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Weapon2, "bow_x", Seed);
        Assert.True(o.HasWeaponBlock);
        Assert.Equal(new[] { 0, 1, 2, 3 }, o.Slots.Select(kv => kv.Key));
        Assert.True(o.TryGet(0, out var w0)); Assert.Equal("spear_a", w0);
        Assert.True(o.TryGet(1, out var w1)); Assert.Equal("shield_a", w1);
        Assert.True(o.TryGet(2, out var w2)); Assert.Equal("bow_x", w2);
        Assert.True(o.TryGet(3, out var w3)); Assert.Equal(GearSlots.Empty, w3);
    }

    [Fact]
    public void Without_seed_the_rest_of_the_block_is_empty()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Weapon0, "sword_x");
        Assert.Equal(new[] { "sword_x", GearSlots.Empty, GearSlots.Empty, GearSlots.Empty }, o.Slots.Select(kv => kv.Value));
    }

    [Fact]
    public void Later_weapon_edits_keep_earlier_ones()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Weapon2, "bow_x", Seed);
        o.Set(GearSlots.Weapon3, "arrows_x", Seed);
        o.Set(GearSlots.Weapon0, GearSlots.Empty, Seed);
        Assert.Equal(new[] { GearSlots.Empty, "shield_a", "bow_x", "arrows_x" }, o.Slots.Select(kv => kv.Value));
    }

    [Fact]
    public void Horse_and_harness_are_a_pair()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Harness, "harness_x", Seed);
        Assert.True(o.HasMountPair);
        Assert.True(o.TryGet(GearSlots.Horse, out var horse)); Assert.Equal("horse_a", horse);
        Assert.False(o.HasWeaponBlock);
    }

    [Fact]
    public void Reverting_one_weapon_reverts_the_block()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Weapon1, "shield_x", Seed);
        o.Set(GearSlots.Head, "helmet_x", Seed);
        o.Revert(GearSlots.Weapon3);
        Assert.False(o.HasWeaponBlock);
        Assert.Single(o.Slots);
    }

    [Theory]
    [InlineData(4)]   // banner slot
    [InlineData(12)]
    [InlineData(-1)]
    public void Bad_slots_throw(int slot)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SetOverride().Set(slot, "x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a|b")]
    [InlineData("a b")]
    public void Bad_item_ids_throw(string id)
    {
        Assert.Throws<ArgumentException>(() => new SetOverride().Set(GearSlots.Head, id));
    }

    [Fact]
    public void Bad_seed_ids_become_empty()
    {
        var o = new SetOverride();
        o.Set(GearSlots.Weapon0, "sword_x", s => s == 1 ? "has|pipe" : null);
        Assert.True(o.TryGet(1, out var w1));
        Assert.Equal(GearSlots.Empty, w1);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("weapon3", 3)]
    [InlineData("HEAD", 5)]
    [InlineData(" harness ", 11)]
    public void Slot_names_parse(string text, int expected)
    {
        Assert.True(GearSlots.TryParse(text, out var slot));
        Assert.Equal(expected, slot);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("banner")]
    [InlineData("")]
    public void Bad_slot_names_do_not_parse(string text)
    {
        Assert.False(GearSlots.TryParse(text, out _));
    }
}

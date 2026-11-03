using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

/// <summary>Which slots count as edited (Toni's test, 2026-11-03: editing weapon 3 marked all 4 weapons as edited).</summary>
public class EditedSlotTests
{
    private static readonly Dictionary<int, string> Vanilla = new()
    {
        [GearSlots.Weapon0] = "spear_a",
        [GearSlots.Weapon1] = "shield_a",
        [GearSlots.Weapon2] = "javelin_a",
        [GearSlots.Horse] = "horse_a",
        [GearSlots.Harness] = "harness_a",
        [GearSlots.Head] = "cap_a",
    };

    private static string? Seed(int slot) => Vanilla.TryGetValue(slot, out var id) ? id : null;

    [Fact]
    public void Editing_one_weapon_marks_only_that_weapon()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", GearSlots.Weapon2, "bow_x", Seed);
        var o = store.Get("t");
        Assert.True(GearEditing.IsSlotEdited(o, GearSlots.Weapon2, Seed));
        Assert.False(GearEditing.IsSlotEdited(o, GearSlots.Weapon0, Seed));
        Assert.False(GearEditing.IsSlotEdited(o, GearSlots.Weapon1, Seed));
        Assert.False(GearEditing.IsSlotEdited(o, GearSlots.Weapon3, Seed)); // vanilla empty, seeded empty
        Assert.Equal(1, GearEditing.EditedSlotCount(o, Seed));
        Assert.True(GearEditing.HasEdits(o, Seed));
    }

    [Fact]
    public void Emptying_a_vanilla_weapon_is_an_edit()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", GearSlots.Weapon0, GearSlots.Empty, Seed);
        Assert.True(GearEditing.IsSlotEdited(store.Get("t"), GearSlots.Weapon0, Seed));
    }

    [Fact]
    public void Armour_slot_is_edited_whenever_stored_even_with_the_first_sets_item()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", GearSlots.Head, "cap_a", Seed); // pin set 1's helmet on every set
        Assert.True(GearEditing.IsSlotEdited(store.Get("t"), GearSlots.Head, Seed));
        Assert.True(store.HasOverride("t"));
    }

    [Fact]
    public void Picking_the_vanilla_weapon_back_drops_the_block()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", GearSlots.Weapon2, "bow_x", Seed);
        GearEditing.SetSlot(store, "t", GearSlots.Weapon2, "javelin_a", Seed);
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void Resetting_the_edited_weapon_drops_the_block_and_keeps_armour()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", GearSlots.Weapon2, "bow_x", Seed);
        GearEditing.SetSlot(store, "t", GearSlots.Body, "mail_x", Seed);
        Assert.True(GearEditing.RevertSlot(store, "t", GearSlots.Weapon2, Seed));
        var o = store.Get("t")!;
        Assert.False(o.HasWeaponBlock);
        Assert.True(o.IsOverridden(GearSlots.Body));
    }

    [Fact]
    public void Mount_pair_follows_the_same_rule()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", GearSlots.Harness, "harness_x", Seed);
        var o = store.Get("t");
        Assert.True(GearEditing.IsSlotEdited(o, GearSlots.Harness, Seed));
        Assert.False(GearEditing.IsSlotEdited(o, GearSlots.Horse, Seed));
        GearEditing.RevertSlot(store, "t", GearSlots.Harness, Seed);
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void A_stored_all_vanilla_block_has_no_edits_and_is_dropped_on_request()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Weapon0, "spear_a", Seed); // e.g. from the console or an old save
        Assert.False(GearEditing.HasEdits(store.Get("t"), Seed));
        Assert.True(GearEditing.DropVanillaBlocks(store, "t", Seed));
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void No_override_no_edits()
    {
        Assert.False(GearEditing.IsSlotEdited(null, GearSlots.Head, Seed));
        Assert.Equal(0, GearEditing.EditedSlotCount(null, Seed));
    }

    [Fact]
    public void Texts()
    {
        Assert.Equal("Tier 3 | 1,250", GearEditing.TierPriceText(3, 1250));
    }
}

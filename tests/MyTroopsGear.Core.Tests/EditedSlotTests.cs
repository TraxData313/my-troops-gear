using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

/// <summary>Which slots count as edited (Toni's test, 2026-11-03: editing weapon 3 marked all 4 weapons as edited),
/// now per vanilla set.</summary>
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
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon2, "bow_x", Seed);
        var o = store.GetSet("t", 0);
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
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon0, GearSlots.Empty, Seed);
        Assert.True(GearEditing.IsSlotEdited(store.GetSet("t", 0), GearSlots.Weapon0, Seed));
    }

    [Fact]
    public void Picking_the_sets_own_armour_back_is_vanilla_again()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Head, "helmet_x", Seed);
        Assert.True(GearEditing.IsSlotEdited(store.GetSet("t", 0), GearSlots.Head, Seed));
        GearEditing.SetSlot(store, "t", 0, GearSlots.Head, "cap_a", Seed);
        Assert.False(store.HasOverride("t"));
        GearEditing.SetSlot(store, "t", 0, GearSlots.Body, GearSlots.Empty, Seed); // vanilla body is empty too
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void Picking_the_vanilla_weapon_back_drops_the_block()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon2, "bow_x", Seed);
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon2, "javelin_a", Seed);
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void Resetting_the_edited_weapon_drops_the_block_and_keeps_armour()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon2, "bow_x", Seed);
        GearEditing.SetSlot(store, "t", 0, GearSlots.Body, "mail_x", Seed);
        Assert.True(GearEditing.RevertSlot(store, "t", 0, GearSlots.Weapon2, Seed));
        var o = store.GetSet("t", 0)!;
        Assert.False(o.HasWeaponBlock);
        Assert.True(o.IsOverridden(GearSlots.Body));
    }

    [Fact]
    public void Mount_pair_follows_the_same_rule()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Harness, "harness_x", Seed);
        var o = store.GetSet("t", 0);
        Assert.True(GearEditing.IsSlotEdited(o, GearSlots.Harness, Seed));
        Assert.False(GearEditing.IsSlotEdited(o, GearSlots.Horse, Seed));
        GearEditing.RevertSlot(store, "t", 0, GearSlots.Harness, Seed);
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void Stored_unchanged_slots_have_no_edits_and_are_dropped_on_request()
    {
        var store = new OverrideStore();
        store.Set("t", 0, GearSlots.Weapon0, "spear_a", Seed); // e.g. from an old save
        store.Set("t", 0, GearSlots.Head, "cap_a");
        Assert.False(GearEditing.HasEdits(store.GetSet("t", 0), Seed));
        Assert.True(GearEditing.DropUnchanged(store, "t", 0, Seed));
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void No_override_no_edits()
    {
        Assert.False(GearEditing.IsSlotEdited(null, GearSlots.Head, Seed));
        Assert.Equal(0, GearEditing.EditedSlotCount((SetOverride?)null, Seed));
        Assert.False(GearEditing.HasEdits((TroopOverride?)null, (_, s) => Seed(s)));
    }

    [Fact]
    public void Texts()
    {
        Assert.Equal("Tier 3 | 1,250", GearEditing.TierPriceText(3, 1250));
    }
}

/// <summary>Per-set editing (Toni, 2026-11-03): one Battanian Volunteer set has a hatchet, another a spear, one gloves, another none.</summary>
public class PerSetEditingTests
{
    // set 0: hatchet + gloves, set 1: spear, no gloves, set 2: hatchet, no gloves
    private static readonly Dictionary<int, string>[] Sets =
    {
        new() { [GearSlots.Weapon0] = "hatchet", [GearSlots.Gloves] = "gloves", [GearSlots.Head] = "hood" },
        new() { [GearSlots.Weapon0] = "spear", [GearSlots.Head] = "hood" },
        new() { [GearSlots.Weapon0] = "hatchet", [GearSlots.Head] = "cap" },
    };

    private static string? V(int set, int slot) => Sets[set].TryGetValue(slot, out var id) ? id : null;

    private static Func<int, string?> Of(int set) => s => V(set, s);

    [Fact]
    public void An_edit_of_one_set_leaves_the_others_vanilla()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 1, GearSlots.Weapon0, "falx", Of(1));
        Assert.Null(store.GetSet("t", 0));
        Assert.Null(store.GetSet("t", 2));
        Assert.Equal("falx", GearEditing.EffectiveItem(store.GetSet("t", 1), GearSlots.Weapon0, Of(1)));
        Assert.Equal(1, GearEditing.EditedSlotCount(store.Get("t"), V));
        Assert.True(GearEditing.HasEdits(store.Get("t"), V));
    }

    [Fact]
    public void Edited_is_judged_against_that_sets_own_vanilla_item()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 1, GearSlots.Weapon0, "hatchet", Of(1)); // set 0's weapon on set 1: an edit there
        Assert.True(GearEditing.IsSlotEdited(store.GetSet("t", 1), GearSlots.Weapon0, Of(1)));
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon0, "hatchet", Of(0)); // set 0's own weapon: nothing
        Assert.Null(store.GetSet("t", 0));
    }

    [Fact]
    public void All_sets_copies_the_rows_item_onto_every_set()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Head, "great_helm", Of(0));
        Assert.True(GearEditing.CanApplyToAllSets(store.Get("t"), 0, GearSlots.Head, 3, V));
        Assert.Equal(2, GearEditing.ApplyToAllSets(store, "t", 0, GearSlots.Head, 3, V));
        for (int k = 0; k < 3; k++)
            Assert.Equal("great_helm", GearEditing.EffectiveItem(store.GetSet("t", k), GearSlots.Head, Of(k)));
        Assert.False(GearEditing.CanApplyToAllSets(store.Get("t"), 1, GearSlots.Head, 3, V));
    }

    [Fact]
    public void All_sets_with_a_vanilla_item_spreads_it_and_reverts_where_it_is_vanilla()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 2, GearSlots.Head, "hood", Of(2)); // set 2 gets set 0/1's hood
        // from set 0 (vanilla hood): set 1 already has it, set 2 has it as an edit -> nothing to do
        Assert.False(GearEditing.CanApplyToAllSets(store.Get("t"), 0, GearSlots.Head, 3, V));
        // gloves of set 0 (vanilla) onto sets 1 and 2
        Assert.True(GearEditing.CanApplyToAllSets(store.Get("t"), 0, GearSlots.Gloves, 3, V));
        Assert.Equal(2, GearEditing.ApplyToAllSets(store, "t", 0, GearSlots.Gloves, 3, V));
        Assert.Null(store.GetSet("t", 0));
        Assert.True(store.GetSet("t", 1)!.TryGet(GearSlots.Gloves, out var g1)); Assert.Equal("gloves", g1);
        // set 1's empty gloves slot back to all: set 0 and 2 lose gloves; set 2 reverts to its vanilla none
        GearEditing.RevertSlot(store, "t", 1, GearSlots.Gloves, Of(1));
        GearEditing.ApplyToAllSets(store, "t", 1, GearSlots.Gloves, 3, V);
        Assert.True(store.GetSet("t", 0)!.TryGet(GearSlots.Gloves, out var g0)); Assert.Equal(GearSlots.Empty, g0);
        Assert.False(store.GetSet("t", 2)!.IsOverridden(GearSlots.Gloves));
    }

    [Fact]
    public void All_sets_on_a_weapon_keeps_each_sets_other_weapons()
    {
        var sets = new[]
        {
            new Dictionary<int, string> { [GearSlots.Weapon0] = "hatchet", [GearSlots.Weapon1] = "shield" },
            new Dictionary<int, string> { [GearSlots.Weapon0] = "spear", [GearSlots.Weapon1] = "javelins" },
        };
        string? v(int set, int slot) => sets[set].TryGetValue(slot, out var id) ? id : null;
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Weapon0, "falx", s => v(0, s));
        GearEditing.ApplyToAllSets(store, "t", 0, GearSlots.Weapon0, 2, v);
        var s1 = store.GetSet("t", 1)!;
        Assert.True(s1.TryGet(GearSlots.Weapon0, out var w0)); Assert.Equal("falx", w0);
        Assert.True(s1.TryGet(GearSlots.Weapon1, out var w1)); Assert.Equal("javelins", w1);
    }

    [Fact]
    public void Single_set_troop_has_no_all_sets()
    {
        Assert.False(GearEditing.CanApplyToAllSets(null, 0, GearSlots.Head, 1, V));
        Assert.Equal(0, GearEditing.ApplyToAllSets(new OverrideStore(), "t", 0, GearSlots.Head, 1, V));
    }

    [Fact]
    public void Reset_set_and_reset_troop()
    {
        var store = new OverrideStore();
        GearEditing.SetSlot(store, "t", 0, GearSlots.Head, "great_helm", Of(0));
        GearEditing.SetSlot(store, "t", 2, GearSlots.Body, "mail", Of(2));
        Assert.True(GearEditing.ResetSet(store, "t", 0));
        Assert.False(GearEditing.ResetSet(store, "t", 0));
        Assert.Equal(new[] { 2 }, store.Get("t")!.SetIndices);
        Assert.True(GearEditing.ResetTroop(store, "t"));
        Assert.False(store.HasOverride("t"));
    }
}

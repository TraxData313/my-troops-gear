using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

public class GearEditingTests
{
    private static readonly Dictionary<int, string> Vanilla = new()
    {
        [GearSlots.Weapon0] = "spear_a",
        [GearSlots.Weapon1] = "shield_a",
        [GearSlots.Horse] = "horse_a",
        [GearSlots.Harness] = "harness_a",
        [GearSlots.Head] = "cap_a",
    };

    private static string? Seed(int slot) => Vanilla.TryGetValue(slot, out var id) ? id : null;

    [Fact]
    public void Armour_revert_drops_the_slot()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Head, "helmet_x", Seed);
        store.Set("t", GearSlots.Body, "mail_x", Seed);
        Assert.True(GearEditing.RevertSlot(store, "t", GearSlots.Head, Seed));
        Assert.False(store.Get("t")!.IsOverridden(GearSlots.Head));
        Assert.True(store.Get("t")!.IsOverridden(GearSlots.Body));
    }

    [Fact]
    public void Reverting_the_last_slot_removes_the_troop()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Head, "helmet_x", Seed);
        GearEditing.RevertSlot(store, "t", GearSlots.Head, Seed);
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void Weapon_revert_sets_that_slot_to_vanilla_and_keeps_the_rest_of_the_block()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Weapon0, "sword_x", Seed);
        store.Set("t", GearSlots.Weapon2, "bow_x", Seed);
        Assert.True(GearEditing.RevertSlot(store, "t", GearSlots.Weapon0, Seed));
        var o = store.Get("t")!;
        Assert.True(o.TryGet(GearSlots.Weapon0, out var w0));
        Assert.Equal("spear_a", w0);
        Assert.True(o.TryGet(GearSlots.Weapon2, out var w2));
        Assert.Equal("bow_x", w2);
    }

    [Fact]
    public void Weapon_revert_of_a_vanilla_empty_slot_sets_empty()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Weapon0, "sword_x", Seed);
        store.Set("t", GearSlots.Weapon3, "javelin_x", Seed);
        GearEditing.RevertSlot(store, "t", GearSlots.Weapon3, Seed);
        Assert.True(store.Get("t")!.TryGet(GearSlots.Weapon3, out var w3));
        Assert.Equal(GearSlots.Empty, w3);
    }

    [Fact]
    public void Block_back_on_all_vanilla_items_goes_back_to_the_random_roll()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Weapon0, "sword_x", Seed);
        store.Set("t", GearSlots.Head, "helmet_x", Seed);
        GearEditing.RevertSlot(store, "t", GearSlots.Weapon0, Seed);
        var o = store.Get("t")!;
        Assert.False(o.HasWeaponBlock);
        Assert.True(o.IsOverridden(GearSlots.Head));
    }

    [Fact]
    public void Mount_pair_revert_follows_the_same_rule()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Horse, "warhorse_x", Seed);
        store.Set("t", GearSlots.Harness, "barding_x", Seed);
        GearEditing.RevertSlot(store, "t", GearSlots.Horse, Seed);
        Assert.True(store.Get("t")!.TryGet(GearSlots.Horse, out var h));
        Assert.Equal("horse_a", h);
        GearEditing.RevertSlot(store, "t", GearSlots.Harness, Seed);
        Assert.False(store.HasOverride("t"));
    }

    [Fact]
    public void Revert_of_an_untouched_slot_does_nothing()
    {
        var store = new OverrideStore();
        Assert.False(GearEditing.RevertSlot(store, "t", GearSlots.Head, Seed));
        store.Set("t", GearSlots.Body, "mail_x", Seed);
        Assert.False(GearEditing.RevertSlot(store, "t", GearSlots.Weapon1, Seed));
        Assert.Single(store.Get("t")!.Slots);
    }

    [Fact]
    public void Reset_troop_clears_everything()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Weapon0, "sword_x", Seed);
        store.Set("t", GearSlots.Head, "helmet_x", Seed);
        Assert.True(GearEditing.ResetTroop(store, "t"));
        Assert.False(store.HasOverride("t"));
        Assert.False(GearEditing.ResetTroop(store, "t"));
    }

    [Fact]
    public void Editor_order_covers_every_slot_once()
    {
        Assert.Equal(GearSlots.All.OrderBy(s => s), GearEditing.EditorOrder.OrderBy(s => s));
    }
}

public class ItemCandidateTests
{
    private static List<ItemCandidate> Sample() => new()
    {
        new ItemCandidate("bat_spear", "Battanian Spear", 2, "Polearm", "battania"),
        new ItemCandidate("bat_axe", "Highland Axe", 3, "OneHandedWeapon", "battania"),
        new ItemCandidate("emp_sword", "Imperial Sword", 4, "OneHandedWeapon", "empire"),
        new ItemCandidate("plain_club", "Club", 1, "OneHandedWeapon"),
        new ItemCandidate("noble_sword", "Noble Sword", 6, "OneHandedWeapon", "empire"),
        new ItemCandidate("crafted", "My Blade", 5, "OneHandedWeapon") { IsCraftedByPlayer = true },
        new ItemCandidate("quest_thing", "Quest Relic", 2, "OneHandedWeapon") { NotMerchandise = true },
        new ItemCandidate("guard_spear", "Guard Spear", 3, "Polearm", "battania") { NotMerchandise = true, UsedByTroops = true },
        new ItemCandidate("mens_cloak", "Men's Cloak", 1, "Cloth") { NotUsableByFemale = true },
        new ItemCandidate("dress", "Dress", 1, "Cloth") { NotUsableByMale = true },
    };

    private static List<string> Ids(IEnumerable<ItemCandidate> list) => list.Select(c => c.Id).ToList();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(5, 6)]
    [InlineData(9, 6)]
    public void Default_tier_cap_is_one_above_the_troop(int troopTier, int expected) =>
        Assert.Equal(expected, ItemCandidates.DefaultMaxTier(troopTier));

    [Fact]
    public void Game_tier_enum_counts_from_one() => Assert.Equal(1, ItemCandidates.TierFromGame(0));

    [Fact]
    public void Crafted_quest_and_wrong_gender_items_are_never_offered()
    {
        var male = Ids(ItemCandidates.Filter(Sample(), new CandidateQuery()));
        Assert.DoesNotContain("crafted", male);
        Assert.DoesNotContain("quest_thing", male);
        Assert.Contains("guard_spear", male); // not sold, but a troop wears it
        Assert.Contains("mens_cloak", male);
        Assert.DoesNotContain("dress", male);

        var female = Ids(ItemCandidates.Filter(Sample(), new CandidateQuery { IsFemale = true }));
        Assert.Contains("dress", female);
        Assert.DoesNotContain("mens_cloak", female);
    }

    [Fact]
    public void Tier_cap_and_sorting()
    {
        var list = ItemCandidates.Filter(Sample(), new CandidateQuery { MaxTier = 3 });
        Assert.All(list, c => Assert.True(c.Tier <= 3));
        Assert.Equal(list.OrderBy(c => c.Tier).ThenBy(c => c.Name).Select(c => c.Id), Ids(list));
        Assert.Equal("Club", list[0].Name);
    }

    [Fact]
    public void Culture_modes()
    {
        var all = Sample();
        var troopAndNeutral = Ids(ItemCandidates.Filter(all, new CandidateQuery { Culture = CultureMode.CultureAndNeutral, CultureId = "battania" }));
        Assert.Contains("bat_spear", troopAndNeutral);
        Assert.Contains("plain_club", troopAndNeutral);
        Assert.DoesNotContain("emp_sword", troopAndNeutral);

        var only = Ids(ItemCandidates.Filter(all, new CandidateQuery { Culture = CultureMode.CultureOnly, CultureId = "empire" }));
        Assert.Equal(new[] { "emp_sword", "noble_sword" }, only);

        var neutral = Ids(ItemCandidates.Filter(all, new CandidateQuery { Culture = CultureMode.NeutralOnly }));
        Assert.Contains("plain_club", neutral);
        Assert.DoesNotContain("bat_spear", neutral);
    }

    [Fact]
    public void Sub_type_and_search()
    {
        var all = Sample();
        var polearms = Ids(ItemCandidates.Filter(all, new CandidateQuery { SubType = "Polearm" }));
        Assert.Equal(new[] { "bat_spear", "guard_spear" }, polearms);

        var found = Ids(ItemCandidates.Filter(all, new CandidateQuery { Search = "  SWORD " }));
        Assert.Equal(new[] { "emp_sword", "noble_sword" }, found);

        var byId = Ids(ItemCandidates.Filter(all, new CandidateQuery { Search = "bat_" }));
        Assert.Equal(new[] { "bat_spear", "bat_axe" }, byId);
    }

    [Fact]
    public void Filter_option_lists_come_from_allowed_items_only()
    {
        var all = Sample();
        Assert.Equal(new[] { "Cloth", "OneHandedWeapon", "Polearm" }, ItemCandidates.SubTypes(all, false));
        Assert.Equal(new[] { "battania", "empire" }, ItemCandidates.Cultures(all, false));
    }
}

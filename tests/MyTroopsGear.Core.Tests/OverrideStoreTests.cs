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
        var o = new TroopOverride();
        o.Set(GearSlots.Head, "helmet_x", Seed);
        Assert.Single(o.Slots);
        Assert.True(o.IsOverridden(GearSlots.Head));
        Assert.False(o.HasWeaponBlock);
    }

    [Fact]
    public void First_weapon_edit_takes_the_whole_block_seeded_from_vanilla()
    {
        var o = new TroopOverride();
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
        var o = new TroopOverride();
        o.Set(GearSlots.Weapon0, "sword_x");
        Assert.Equal(new[] { "sword_x", GearSlots.Empty, GearSlots.Empty, GearSlots.Empty }, o.Slots.Select(kv => kv.Value));
    }

    [Fact]
    public void Later_weapon_edits_keep_earlier_ones()
    {
        var o = new TroopOverride();
        o.Set(GearSlots.Weapon2, "bow_x", Seed);
        o.Set(GearSlots.Weapon3, "arrows_x", Seed);
        o.Set(GearSlots.Weapon0, GearSlots.Empty, Seed);
        Assert.Equal(new[] { GearSlots.Empty, "shield_a", "bow_x", "arrows_x" }, o.Slots.Select(kv => kv.Value));
    }

    [Fact]
    public void Horse_and_harness_are_a_pair()
    {
        var o = new TroopOverride();
        o.Set(GearSlots.Harness, "harness_x", Seed);
        Assert.True(o.HasMountPair);
        Assert.True(o.TryGet(GearSlots.Horse, out var horse)); Assert.Equal("horse_a", horse);
        Assert.False(o.HasWeaponBlock);
    }

    [Fact]
    public void Reverting_one_weapon_reverts_the_block()
    {
        var o = new TroopOverride();
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
        Assert.Throws<ArgumentOutOfRangeException>(() => new TroopOverride().Set(slot, "x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a|b")]
    [InlineData("a b")]
    public void Bad_item_ids_throw(string id)
    {
        Assert.Throws<ArgumentException>(() => new TroopOverride().Set(GearSlots.Head, id));
    }

    [Fact]
    public void Bad_seed_ids_become_empty()
    {
        var o = new TroopOverride();
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

public class StoreTests
{
    [Fact]
    public void Absent_troop_is_vanilla()
    {
        var store = new OverrideStore();
        Assert.Null(store.Get("imperial_recruit"));
        Assert.False(store.HasOverride("imperial_recruit"));
        Assert.False(store.HasOverride(null));
    }

    [Fact]
    public void Reverting_last_slot_removes_the_troop()
    {
        var store = new OverrideStore();
        store.Set("t", GearSlots.Head, "helmet");
        store.Revert("t", GearSlots.Head);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Bad_set_does_not_add_an_empty_troop()
    {
        var store = new OverrideStore();
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Set("t", 4, "banner"));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Changed_fires_on_edits()
    {
        var store = new OverrideStore();
        int n = 0;
        store.Changed += () => n++;
        store.Set("t", GearSlots.Head, "helmet");
        store.Clear("t");
        store.Clear("t");   // nothing to clear: no event
        store.ClearAll();   // empty: no event
        Assert.Equal(2, n);
    }

    [Fact]
    public void Clear_one_and_all()
    {
        var store = new OverrideStore();
        store.Set("a", GearSlots.Head, "h");
        store.Set("b", GearSlots.Body, "b");
        Assert.True(store.Clear("a"));
        Assert.False(store.Clear("a"));
        Assert.Equal(new[] { "b" }, store.TroopIds);
        store.ClearAll();
        Assert.Equal(0, store.Count);
    }
}

public class SaveLineTests
{
    [Fact]
    public void Round_trip_keeps_everything()
    {
        var store = new OverrideStore();
        store.Set("battanian_wildling", GearSlots.Weapon1, "falx", s => s == 0 ? "javelin" : null);
        store.Set("battanian_wildling", GearSlots.Head, GearSlots.Empty);
        store.Set("imperial_recruit", GearSlots.Horse, "sumpter_horse");

        var lines = store.ToLines();
        Assert.Contains("battanian_wildling|0|javelin", lines);
        Assert.Contains("battanian_wildling|1|falx", lines);
        Assert.Contains("battanian_wildling|2|empty", lines);
        Assert.Contains("battanian_wildling|5|empty", lines);
        Assert.Contains("imperial_recruit|11|empty", lines);
        Assert.Equal(7, lines.Count);

        var back = OverrideStore.FromLines(lines, out var skipped);
        Assert.Equal(0, skipped);
        Assert.Equal(lines, back.ToLines());
    }

    [Fact]
    public void Empty_or_null_input_gives_empty_store()
    {
        Assert.Equal(0, OverrideStore.FromLines(null).Count);
        Assert.Equal(0, OverrideStore.FromLines(new List<string?>()).Count);
    }

    [Fact]
    public void Bad_lines_are_skipped()
    {
        var lines = new List<string?>
        {
            null, "", "   ", "garbage", "a|b", "a|5|x|y", "a|four|x", "a|4|banner", "a|12|x", "|5|x", "a|5|",
            "a b|5|x", "good|5|helmet",
        };
        var store = OverrideStore.FromLines(lines, out var skipped);
        Assert.Equal(12, skipped);
        Assert.Equal(new[] { "good|5|helmet" }, store.ToLines());
    }

    [Fact]
    public void Whitespace_around_fields_is_tolerated()
    {
        var store = OverrideStore.FromLines(new[] { "  t | 6 | armor  " });
        Assert.Equal(new[] { "t|6|armor" }, store.ToLines());
    }

    [Fact]
    public void Repeated_slot_last_line_wins()
    {
        var store = OverrideStore.FromLines(new[] { "t|5|old", "t|5|new" });
        Assert.Equal(new[] { "t|5|new" }, store.ToLines());
    }

    [Fact]
    public void Partial_blocks_are_completed_with_empty()
    {
        var store = OverrideStore.FromLines(new[] { "t|2|bow", "t|10|horse" });
        Assert.Equal(new[] { "t|0|empty", "t|1|empty", "t|2|bow", "t|3|empty", "t|10|horse", "t|11|empty" }, store.ToLines());
    }
}

public class PruneTests
{
    private static OverrideStore Sample() => OverrideStore.FromLines(new[]
    {
        "keep|0|spear", "keep|1|gone_shield", "keep|2|empty", "keep|3|empty",
        "keep|5|gone_helmet", "keep|6|armor",
        "keep|10|horse", "keep|11|harness",
        "removed_troop|5|helmet",
        "only_bad|7|gone_boots",
    });

    [Fact]
    public void Unknown_troops_and_items_are_dropped_by_block()
    {
        var store = Sample();
        var report = store.Prune(t => t != "removed_troop", i => !i.StartsWith("gone_"));

        // unknown shield -> all four weapons vanilla; unknown helmet -> only head; mount pair untouched
        Assert.Equal(new[] { "keep|6|armor", "keep|10|horse", "keep|11|harness" }, store.ToLines());
        Assert.False(store.HasOverride("removed_troop"));
        Assert.False(store.HasOverride("only_bad")); // nothing left -> troop dropped
        Assert.Equal(3, report.Count);
    }

    [Fact]
    public void Empty_marker_is_never_looked_up()
    {
        var store = OverrideStore.FromLines(new[] { "t|5|empty" });
        var report = store.Prune(_ => true, i => throw new InvalidOperationException("looked up " + i));
        Assert.Empty(report);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Nothing_to_prune_reports_nothing()
    {
        var store = Sample();
        int changed = 0;
        store.Changed += () => changed++;
        Assert.Empty(store.Prune(_ => true, _ => true));
        Assert.Equal(0, changed);
    }
}

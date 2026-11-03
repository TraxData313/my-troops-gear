using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

public class StoreTests
{
    [Fact]
    public void Absent_troop_is_vanilla()
    {
        var store = new OverrideStore();
        Assert.Null(store.Get("imperial_recruit"));
        Assert.Null(store.GetSet("imperial_recruit", 0));
        Assert.False(store.HasOverride("imperial_recruit"));
        Assert.False(store.HasOverride(null));
    }

    [Fact]
    public void Sets_are_independent()
    {
        var store = new OverrideStore();
        store.Set("t", 0, GearSlots.Head, "helmet_a");
        store.Set("t", 2, GearSlots.Head, "helmet_c");
        Assert.True(store.GetSet("t", 0)!.TryGet(GearSlots.Head, out var h0)); Assert.Equal("helmet_a", h0);
        Assert.Null(store.GetSet("t", 1));
        Assert.True(store.GetSet("t", 2)!.TryGet(GearSlots.Head, out var h2)); Assert.Equal("helmet_c", h2);
        Assert.Equal(new[] { 0, 2 }, store.Get("t")!.SetIndices);
    }

    [Fact]
    public void Reverting_last_slot_removes_the_set_then_the_troop()
    {
        var store = new OverrideStore();
        store.Set("t", 0, GearSlots.Head, "helmet");
        store.Set("t", 1, GearSlots.Head, "helmet");
        store.Revert("t", 0, GearSlots.Head);
        Assert.Null(store.GetSet("t", 0));
        Assert.True(store.HasOverride("t"));
        store.Revert("t", 1, GearSlots.Head);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Bad_set_slot_or_item_adds_nothing()
    {
        var store = new OverrideStore();
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Set("t", 0, 4, "banner"));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Set("t", -1, GearSlots.Head, "h"));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Set("t", TroopOverride.MaxSets, GearSlots.Head, "h"));
        Assert.Throws<ArgumentException>(() => store.Set("t", 0, GearSlots.Head, "a b"));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Changed_fires_on_edits()
    {
        var store = new OverrideStore();
        int n = 0;
        store.Changed += () => n++;
        store.Set("t", 0, GearSlots.Head, "helmet");
        store.Set("t", 1, GearSlots.Head, "helmet");
        store.ClearSet("t", 1);
        store.ClearSet("t", 1);  // nothing: no event
        store.Clear("t");
        store.Clear("t");        // nothing to clear: no event
        store.ClearAll();        // empty: no event
        Assert.Equal(4, n);
    }

    [Fact]
    public void Clear_set_one_troop_and_all()
    {
        var store = new OverrideStore();
        store.Set("a", 0, GearSlots.Head, "h");
        store.Set("a", 1, GearSlots.Head, "h");
        store.Set("b", 0, GearSlots.Body, "b");
        Assert.True(store.ClearSet("a", 0));
        Assert.Equal(new[] { 1 }, store.Get("a")!.SetIndices);
        Assert.True(store.ClearSet("a", 1));
        Assert.False(store.HasOverride("a"));
        Assert.False(store.Clear("a"));
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
        store.Set("battanian_wildling", 1, GearSlots.Weapon1, "falx", s => s == 0 ? "javelin" : null);
        store.Set("battanian_wildling", 0, GearSlots.Head, GearSlots.Empty);
        store.Set("imperial_recruit", 3, GearSlots.Horse, "sumpter_horse");

        var lines = store.ToLines();
        Assert.Contains("battanian_wildling|1|0|javelin", lines);
        Assert.Contains("battanian_wildling|1|1|falx", lines);
        Assert.Contains("battanian_wildling|1|2|empty", lines);
        Assert.Contains("battanian_wildling|0|5|empty", lines);
        Assert.Contains("imperial_recruit|3|11|empty", lines);
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
            null, "", "   ", "garbage", "a|b", "a|0|5|x|y", "a|four|x", "a|4|banner", "a|12|x", "|5|x", "a|5|",
            "a b|5|x", "a|x|5|h", "a|-1|5|h", "a|64|5|h", "a|0|4|banner",
            "good|1|5|helmet",
        };
        var store = OverrideStore.FromLines(lines, out var skipped);
        Assert.Equal(16, skipped);
        Assert.Equal(new[] { "good|1|5|helmet" }, store.ToLines());
    }

    [Fact]
    public void Whitespace_around_fields_is_tolerated()
    {
        var store = OverrideStore.FromLines(new[] { "  t | 2 | 6 | armor  " });
        Assert.Equal(new[] { "t|2|6|armor" }, store.ToLines());
    }

    [Fact]
    public void Repeated_line_last_wins()
    {
        var store = OverrideStore.FromLines(new[] { "t|0|5|old", "t|0|5|new", "t|1|5|other" });
        Assert.Equal(new[] { "t|0|5|new", "t|1|5|other" }, store.ToLines());
    }

    [Fact]
    public void Partial_blocks_are_completed_with_empty()
    {
        var store = OverrideStore.FromLines(new[] { "t|1|2|bow", "t|1|10|horse" });
        Assert.Equal(new[] { "t|1|0|empty", "t|1|1|empty", "t|1|2|bow", "t|1|3|empty", "t|1|10|horse", "t|1|11|empty" }, store.ToLines());
    }

    [Fact]
    public void Old_lines_wait_as_all_sets_edits_and_survive_a_save()
    {
        var store = OverrideStore.FromLines(new[] { "t|5|helmet", "t|2|bow" });
        Assert.Equal(0, store.Count);               // not per-set yet
        Assert.Equal(1, store.PendingAllSetsCount);
        Assert.Equal(new[] { "t|0|empty", "t|1|empty", "t|2|bow", "t|3|empty", "t|5|helmet" }, store.ToLines());
    }
}

public class PruneTests
{
    private static int Sets(string troopId) => troopId == "three" ? 3 : 2;

    [Fact]
    public void Old_all_sets_lines_expand_onto_every_set()
    {
        var store = OverrideStore.FromLines(new[] { "three|5|helmet", "three|0|spear" });
        var report = store.Prune(_ => true, _ => true, Sets);
        Assert.Equal(0, store.PendingAllSetsCount);
        Assert.Equal(new[] { 0, 1, 2 }, store.Get("three")!.SetIndices);
        for (int k = 0; k < 3; k++)
        {
            Assert.True(store.GetSet("three", k)!.TryGet(GearSlots.Head, out var h)); Assert.Equal("helmet", h);
            Assert.True(store.GetSet("three", k)!.TryGet(GearSlots.Weapon0, out var w)); Assert.Equal("spear", w);
        }
        Assert.Single(report);
        Assert.All(store.ToLines(), l => Assert.Equal(4, l.Split('|').Length));
    }

    [Fact]
    public void Per_set_lines_win_over_old_all_sets_lines()
    {
        var store = OverrideStore.FromLines(new[] { "t|5|old_helmet", "t|6|old_armor", "t|1|5|set_helmet" });
        store.Prune(_ => true, _ => true, Sets);
        Assert.True(store.GetSet("t", 0)!.TryGet(GearSlots.Head, out var h0)); Assert.Equal("old_helmet", h0);
        Assert.True(store.GetSet("t", 1)!.TryGet(GearSlots.Head, out var h1)); Assert.Equal("set_helmet", h1);
        Assert.True(store.GetSet("t", 1)!.TryGet(GearSlots.Body, out var b1)); Assert.Equal("old_armor", b1);
    }

    [Fact]
    public void Sets_the_troop_no_longer_has_are_dropped()
    {
        var store = OverrideStore.FromLines(new[] { "t|0|5|a", "t|1|5|b", "t|3|5|d", "gone_sets|2|5|x" });
        var report = store.Prune(_ => true, _ => true, Sets);
        Assert.Equal(new[] { 0, 1 }, store.Get("t")!.SetIndices);
        Assert.False(store.HasOverride("gone_sets"));
        Assert.Equal(2, report.Count);
    }

    [Fact]
    public void Unknown_troops_and_items_are_dropped_by_block_per_set()
    {
        var store = OverrideStore.FromLines(new[]
        {
            "keep|0|0|spear", "keep|0|1|gone_shield", "keep|0|2|empty", "keep|0|3|empty",
            "keep|0|5|gone_helmet", "keep|0|6|armor",
            "keep|1|0|spear", "keep|1|1|shield", "keep|1|2|empty", "keep|1|3|empty",
            "removed_troop|0|5|helmet", "removed_troop|5|helmet",
            "only_bad|1|7|gone_boots",
        });
        var report = store.Prune(t => t != "removed_troop", i => !i.StartsWith("gone_"), Sets);

        // set 0: unknown shield -> its weapons vanilla; unknown helmet -> only head. Set 1 untouched.
        Assert.Equal(new[] { "keep|0|6|armor", "keep|1|0|spear", "keep|1|1|shield", "keep|1|2|empty", "keep|1|3|empty" }, store.ToLines());
        Assert.False(store.HasOverride("removed_troop"));
        Assert.Equal(0, store.PendingAllSetsCount);
        Assert.False(store.HasOverride("only_bad"));
        Assert.Equal(3, report.Count);
    }

    [Fact]
    public void Empty_marker_is_never_looked_up()
    {
        var store = OverrideStore.FromLines(new[] { "t|0|5|empty" });
        var report = store.Prune(_ => true, i => throw new InvalidOperationException("looked up " + i), Sets);
        Assert.Empty(report);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Nothing_to_prune_reports_nothing()
    {
        var store = OverrideStore.FromLines(new[] { "t|0|5|a", "t|1|0|spear" });
        int changed = 0;
        store.Changed += () => changed++;
        Assert.Empty(store.Prune(_ => true, _ => true, Sets));
        Assert.Equal(0, changed);
    }
}

public class SpawnSetTests
{
    [Theory]
    [InlineData(12345, 4)]
    [InlineData(7, 3)]
    [InlineData(0, 2)]
    [InlineData(int.MaxValue, 5)]
    public void Seeded_pick_is_vanillas_first_draw(int seed, int count)
    {
        Assert.Equal(new Random(seed).Next() % count, SpawnSet.Pick(seed, count, _ => throw new InvalidOperationException()));
        Assert.Equal(SpawnSet.Pick(seed, count, _ => 0), SpawnSet.Pick(seed, count, _ => 0)); // same soldier, same set
    }

    [Fact]
    public void Edge_cases()
    {
        Assert.Equal(-1, SpawnSet.Pick(5, 0, _ => 0));
        Assert.Equal(0, SpawnSet.Pick(5, 1, _ => 0));
        Assert.Equal(2, SpawnSet.Pick(-1, 3, n => 2));
        Assert.Equal(0, SpawnSet.Pick(-1, 3, n => 9)); // a bad random stays in range
    }

    [Fact]
    public void Seeded_picks_cover_every_set()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 200; seed++)
            seen.Add(SpawnSet.Pick(seed, 4, _ => 0));
        Assert.Equal(new[] { 0, 1, 2, 3 }, seen.OrderBy(x => x));
    }
}

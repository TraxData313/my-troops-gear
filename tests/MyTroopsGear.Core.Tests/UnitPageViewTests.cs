using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

public class UnitPageViewTests
{
    [Theory]
    // wantMine, canShowMine, shownMine, force -> action
    [InlineData(false, false, false, false, UnitPageAction.None)]
    [InlineData(false, true, false, false, UnitPageAction.None)]
    [InlineData(false, true, false, true, UnitPageAction.None)]
    [InlineData(false, true, true, false, UnitPageAction.ShowVanilla)]
    [InlineData(true, true, false, false, UnitPageAction.ShowMine)]
    [InlineData(true, true, true, false, UnitPageAction.None)]
    [InlineData(true, true, true, true, UnitPageAction.ShowMine)]
    [InlineData(true, false, false, false, UnitPageAction.None)]
    [InlineData(true, false, true, false, UnitPageAction.ShowVanilla)] // the whole unit was reset in the editor
    [InlineData(true, false, true, true, UnitPageAction.ShowVanilla)]
    public void Decide_follows_the_choice_only_where_there_is_an_override(
        bool wantMine, bool canShowMine, bool shownMine, bool force, UnitPageAction expected)
    {
        Assert.Equal(expected, UnitPageView.Decide(wantMine, canShowMine, shownMine, force));
    }

    [Fact]
    public void DistinctInOrder_keeps_first_occurrences()
    {
        var kept = UnitPageView.DistinctInOrder(new[] { "a1", "b1", "a2", "c1", "b2" }, (x, y) => x[0] == y[0]);
        Assert.Equal(new[] { "a1", "b1", "c1" }, kept);
    }

    [Fact]
    public void DistinctInOrder_of_nothing_is_empty()
    {
        Assert.Empty(UnitPageView.DistinctInOrder(new string[0], (x, y) => x == y));
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(2, 1, 0)] // edits made all sets equal: back to the one left
    [InlineData(-1, 2, 0)]
    [InlineData(1, 0, -1)]
    public void KeepIndex_clamps_into_the_new_list(int current, int count, int expected)
    {
        Assert.Equal(expected, UnitPageView.KeepIndex(current, count));
    }
}

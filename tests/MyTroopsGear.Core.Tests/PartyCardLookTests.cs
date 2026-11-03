using MyTroopsGear.Core;

namespace MyTroopsGear.Core.Tests;

public class PartyCardLookTests
{
    [Theory]
    // isPrisoner, isHero, hasOverride, sideOwnerIsMine -> mine
    [InlineData(false, false, true, true, true)]   // my troop with edits on my side
    [InlineData(false, false, true, false, false)] // same troop in the other party (or a loot roster)
    [InlineData(true, false, true, true, false)]   // my prisoners are never in my gear
    [InlineData(true, false, true, false, false)]
    [InlineData(false, true, true, true, false)]   // heroes keep their own equipment
    [InlineData(false, false, false, true, false)] // nothing edited
    [InlineData(false, false, false, false, false)]
    public void ShowsMine_only_for_edited_non_hero_members_of_my_side(
        bool isPrisoner, bool isHero, bool hasOverride, bool sideOwnerIsMine, bool expected)
    {
        Assert.Equal(expected, PartyCardLook.ShowsMine(isPrisoner, isHero, hasOverride, sideOwnerIsMine));
    }
}

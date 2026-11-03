namespace MyTroopsGear.Core
{
    /// <summary>
    /// Which look a troop card on the party screen gets (also the upgrade-target previews, which are drawn the
    /// same way). The decision is per roster side: a card shows the edited look only when the party that owns
    /// that side of the screen is one of mine (main party, my clan's parties, my garrisons). Prisoners never
    /// wear my gear, heroes have their own equipment, and a troop without an override has nothing to show.
    /// </summary>
    public static class PartyCardLook
    {
        /// <param name="isPrisoner">The card is in a prisoner roster (either side).</param>
        /// <param name="isHero">The troop is a hero.</param>
        /// <param name="hasOverride">The troop (for an upgrade preview: the upgrade target) has a non-empty override.</param>
        /// <param name="sideOwnerIsMine">The party owning this side of the screen is in my scope. False when the
        /// side has no owner party (a loot or donation roster).</param>
        public static bool ShowsMine(bool isPrisoner, bool isHero, bool hasOverride, bool sideOwnerIsMine) =>
            !isPrisoner && !isHero && hasOverride && sideOwnerIsMine;
    }
}

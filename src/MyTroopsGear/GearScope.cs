using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace MyTroopsGear
{
    /// <summary>Whose troops wear the edited gear. Everyone else on the map keeps vanilla.</summary>
    internal static class GearScope
    {
        /// <summary>
        /// True for the main party, any party of a player-clan hero (clan war parties, caravans), and the
        /// settlement parties of player-clan settlements (garrison, militia, the settlement's own party).
        /// Villager parties are never included.
        /// </summary>
        public static bool IsMine(PartyBase? party)
        {
            if (party == null || Campaign.Current == null) return false;
            var player = Clan.PlayerClan;
            if (player == null) return false;

            var mobile = party.MobileParty;
            if (mobile != null)
            {
                if (mobile.IsMainParty) return true;
                if (mobile.IsGarrison || mobile.IsMilitia)
                    return (mobile.CurrentSettlement ?? mobile.HomeSettlement)?.OwnerClan == player;
                if (mobile.IsVillager) return false;
                return mobile.LeaderHero?.Clan == player || party.Owner?.Clan == player;
            }
            return party.IsSettlement && party.Settlement?.OwnerClan == player;
        }
    }
}

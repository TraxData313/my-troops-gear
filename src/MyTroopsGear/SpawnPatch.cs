using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MyTroopsGear
{
    /// <summary>
    /// Prefix on <c>Mission.SpawnAgent(AgentBuildData, bool)</c>, the one place a troop's battle gear is rolled.
    /// Field battles, sieges and sally-outs, hideouts, banner bearers and War Sails naval battles all spawn
    /// through it with a party agent origin, so this single patch covers them.
    ///
    /// When the agent is a non-hero troop of one of MY parties (<see cref="GearScope"/>) with an override, it
    /// picks one vanilla battle set with the agent's seed (<see cref="MyTroopsGear.Core.SpawnSet"/>: vanilla's own
    /// first draw). If that set is edited, the soldier wears that set whole with its edits (handed to SpawnAgent
    /// through <c>AgentBuildData.Equipment(...)</c>); if not, vanilla rolls its usual per-slot mix untouched. SpawnAgent's own later steps (no
    /// horses in sieges / naval, banner bearer, no-armour flags) still run on it.
    ///
    /// Untouched: anything that already chose its gear (tournaments, arena, practice fights and other mods all
    /// set Equipment or FixedEquipment), civilian missions, heroes, and every party that is not mine.
    /// </summary>
    internal static class SpawnPatch
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool) })
                ?? throw new MissingMethodException("Mission.SpawnAgent(AgentBuildData, bool) not found");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SpawnPatch), nameof(Prefix)));
        }

        // Named like SpawnAgent's own parameter (Harmony binds by name).
        private static void Prefix(Mission __instance, AgentBuildData agentBuildData)
        {
            try
            {
                var store = GearBehavior.Instance?.Store;
                if (store == null || store.Count == 0 || agentBuildData == null || Campaign.Current == null)
                    return;
                if (agentBuildData.AgentOverridenSpawnEquipment != null || agentBuildData.AgentFixedEquipment
                    || agentBuildData.AgentCivilianEquipment)
                    return;
                if (!(agentBuildData.AgentCharacter is CharacterObject troop) || troop.IsHero)
                    return;
                var over = store.Get(troop.StringId);
                if (over == null || over.IsEmpty)
                    return;
                if (__instance != null && __instance.Mode == MissionMode.Tournament)
                    return;
                if (!(agentBuildData.AgentOrigin?.BattleCombatant is PartyBase party) || !GearScope.IsMine(party))
                    return;

                var equipment = GearApplier.SpawnEquipment(troop, over, agentBuildData.AgentEquipmentSeed);
                if (equipment != null)
                    agentBuildData.Equipment(equipment);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("spawn", e);
            }
        }
    }
}

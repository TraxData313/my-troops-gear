using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    ///
    /// Other game versions: beta 1.5.6 no longer has the exact 1.4.8 signature (a player's log, 2026-11-10). So
    /// the target is found by shape, not by exact signature: the 1.4.8 method if it exists, else every
    /// <c>Mission.SpawnAgent</c> overload that takes an <see cref="AgentBuildData"/>, else any such
    /// <c>SpawnAgent</c> in the game's MountAndBlade assembly. The prefix reads the build data from
    /// <c>__args</c> by type, so a renamed or moved parameter still binds. Patching more than one overload is
    /// safe: after the first hit the build data carries an override equipment and every later hit steps aside.
    /// </summary>
    internal static class SpawnPatch
    {
        public static void Apply(Harmony harmony)
        {
            var targets = FindTargets();
            if (targets.Count == 0)
                throw new MissingMethodException("no SpawnAgent method taking AgentBuildData found");

            var prefix = new HarmonyMethod(typeof(SpawnPatch), nameof(Prefix));
            foreach (var target in targets)
            {
                harmony.Patch(target, prefix: prefix);
                ModLog.Info("load", "spawn patch on " + Describe(target));
            }
        }

        private static List<MethodInfo> FindTargets()
        {
            var exact = AccessTools.Method(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool) });
            if (exact != null && Patchable(exact))
                return new List<MethodInfo> { exact };

            var onMission = SpawnAgentMethods(typeof(Mission)).ToList();
            if (onMission.Count > 0)
                return onMission;

            Type[] types;
            try { types = typeof(Mission).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            return types.SelectMany(SpawnAgentMethods).ToList();
        }

        private static IEnumerable<MethodInfo> SpawnAgentMethods(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            MethodInfo[] methods;
            try { methods = type.GetMethods(flags); }
            catch { yield break; }
            foreach (var m in methods)
            {
                if (m.Name == "SpawnAgent" && Patchable(m)
                    && m.GetParameters().Any(p => p.ParameterType == typeof(AgentBuildData)))
                    yield return m;
            }
        }

        private static bool Patchable(MethodInfo m) => !m.IsAbstract && !m.ContainsGenericParameters;

        private static string Describe(MethodInfo m) =>
            m.DeclaringType?.Name + "." + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name)) + ")";

        private static AgentBuildData? BuildDataOf(object[]? args)
        {
            if (args == null) return null;
            foreach (var a in args)
                if (a is AgentBuildData data) return data;
            return null;
        }

        private static void Prefix(object[] __args)
        {
            try
            {
                var store = GearBehavior.Instance?.Store;
                if (store == null || store.Count == 0 || Campaign.Current == null)
                    return;
                var agentBuildData = BuildDataOf(__args);
                if (agentBuildData == null)
                    return;
                if (agentBuildData.AgentOverridenSpawnEquipment != null || agentBuildData.AgentFixedEquipment
                    || agentBuildData.AgentCivilianEquipment)
                    return;
                if (!(agentBuildData.AgentCharacter is CharacterObject troop) || troop.IsHero)
                    return;
                var over = store.Get(troop.StringId);
                if (over == null || over.IsEmpty)
                    return;
                var mission = Mission.Current;
                if (mission != null && mission.Mode == MissionMode.Tournament)
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
